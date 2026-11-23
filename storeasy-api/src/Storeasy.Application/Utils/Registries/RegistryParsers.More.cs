using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// The answers of Maven Central, RubyGems, Packagist and pub.dev (package registries phase 2).
/// </summary>
public static partial class RegistryParsers
{
    private const string _unset = "__unset";

    #region Maven Central

    /// <summary>
    /// Maven Central: the artifact's <c>maven-metadata.xml</c> (<c>versioning/release</c>, <c>versioning/latest</c> and
    /// <c>versioning/versions/version</c>) and, when given, the search answer of <c>search.maven.org</c> with
    /// <c>core=gav</c>, whose <c>response.docs[]</c> carry <c>v</c> and <c>timestamp</c> (epoch milliseconds). Maven Central
    /// has no deprecation, yank or provenance data.
    /// </summary>
    public static RegistryPackageRead ParseMaven(string name, string metadataXml, string searchJson)
    {
        XDocument document;

        try
        {
            document = XDocument.Parse(metadataXml ?? string.Empty);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var versioning = document.Root?.Element("versioning");
        var dates = (Parse(searchJson)?["response"]?["docs"] as JsonArray ?? [])
                    .Select(doc => (Version: Text(doc?["v"]), Millis: doc?["timestamp"] is JsonValue value && value.TryGetValue<long>(out var millis) ? millis : (long?)null))
                    .Where(d => d.Version != null && d.Millis != null)
                    .GroupBy(d => d.Version, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => DateTimeOffset.FromUnixTimeMilliseconds(g.First().Millis.Value).UtcDateTime, StringComparer.Ordinal);
        var read = new RegistryPackageRead
        {
            Name = name,
            LatestVersion = NonEmpty(versioning?.Element("release")?.Value) ?? NonEmpty(versioning?.Element("latest")?.Value),
        };

        foreach (var version in versioning?.Element("versions")?.Elements("version").Select(v => v.Value.Trim()).Where(v => v.Length > 0) ?? [])
            read.Versions.Add(new RegistryVersionRead { Version = version, Published = dates.TryGetValue(version, out var date) ? date : null });

        return read;
    }

    #endregion

    #region RubyGems

    /// <summary>
    /// RubyGems: <c>/api/v1/gems/&lt;name&gt;.json</c> (<c>name</c>, <c>version</c>, <c>source_code_uri</c> or
    /// <c>metadata.source_code_uri</c>, <c>homepage_uri</c>) and <c>/api/v1/versions/&lt;name&gt;.json</c> (<c>number</c>,
    /// <c>created_at</c>, <c>platform</c>, <c>yanked</c> when the answer carries it). A version built for several platforms
    /// is one version.
    /// </summary>
    public static RegistryPackageRead ParseRubyGems(string gemJson, string versionsJson)
    {
        var gem = Parse(gemJson);
        var read = new RegistryPackageRead
        {
            Name = Text(gem?["name"]),
            LatestVersion = Text(gem?["version"]),
            Repository = GitUrl(Text(gem?["source_code_uri"]) ?? Text(gem?["metadata"]?["source_code_uri"])) ?? GitUrl(Text(gem?["homepage_uri"])),
        };

        foreach (var group in (Parse(versionsJson) as JsonArray ?? []).Where(v => v != null).GroupBy(v => Text(v["number"])).Where(g => g.Key != null))
        {
            var item = group.FirstOrDefault(v => Text(v["platform"]) == "ruby") ?? group.First();

            read.Versions.Add(new RegistryVersionRead { Version = group.Key, Published = Date(item["created_at"]), Yanked = group.All(v => Bool(v["yanked"])) });
        }

        return read;
    }

    /// <summary>
    /// RubyGems attestations (<c>/api/v1/attestations/&lt;name&gt;-&lt;version&gt;.json</c>): an array of sigstore bundles,
    /// empty without attestations. The SLSA provenance of a bundle's <c>dsseEnvelope.payload</c> names the repository,
    /// workflow and run; a bundle storeasy cannot read still counts as provenance (without those details).
    /// </summary>
    public static ProvenanceRead ParseRubyGemsAttestations(string json)
    {
        if (Parse(json) is not JsonArray bundles || bundles.Count == 0)
            return null;

        return bundles.Select(b => ProvenanceOfPayload(Text(b?["dsseEnvelope"]?["payload"]))).FirstOrDefault(p => p != null) ?? new ProvenanceRead(null, null, null);
    }

    #endregion

    #region Packagist

    /// <summary>
    /// Packagist p2 metadata (<c>repo.packagist.org/p2/&lt;vendor&gt;/&lt;package&gt;.json</c>, tagged releases only). The
    /// answer is minified (<c>"minified": "composer/2.0"</c>): each version lists only the keys that differ from the version
    /// before it, and <c>__unset</c> removes a key. Reads <c>version</c>, <c>version_normalized</c>, <c>time</c>,
    /// <c>source.url</c> and <c>abandoned</c> (true, or the advised replacement) as the deprecation of the latest version.
    /// The latest version is the newest stable one.
    /// </summary>
    public static RegistryPackageRead ParsePackagist(string name, string json)
    {
        var entries = Parse(json)?["packages"]?[name] as JsonArray ?? [];
        var current = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var expanded = new List<Dictionary<string, JsonNode>>();

        foreach (var entry in entries.OfType<JsonObject>())
        {
            foreach (var (key, value) in entry)
            {
                if (value is JsonValue unset && unset.GetValueKind() == System.Text.Json.JsonValueKind.String && unset.GetValue<string>() == _unset)
                    current.Remove(key);
                else
                    current[key] = value?.DeepClone();
            }

            expanded.Add(new Dictionary<string, JsonNode>(current, StringComparer.Ordinal));
        }

        var read = new RegistryPackageRead { Name = name };

        foreach (var entry in expanded)
        {
            var version = Text(entry.GetValueOrDefault("version"));

            if (version != null)
                read.Versions.Add(new RegistryVersionRead { Version = version, Published = Date(entry.GetValueOrDefault("time")) });
        }

        var latest = expanded.Where(e => Text(e.GetValueOrDefault("version")) != null && IsStableComposer(Text(e.GetValueOrDefault("version_normalized")) ?? Text(e.GetValueOrDefault("version"))))
                             .OrderByDescending(e => Date(e.GetValueOrDefault("time")) ?? DateTime.MinValue)
                             .FirstOrDefault()
                     ?? expanded.FirstOrDefault();

        if (latest == null)
            return read;

        read.LatestVersion = Text(latest.GetValueOrDefault("version"));
        read.Repository = GitUrl(Text(latest.GetValueOrDefault("source")?["url"]));

        var abandoned = latest.GetValueOrDefault("abandoned");
        var replacement = Text(abandoned);

        if (read.Latest != null && (Bool(abandoned) || !string.IsNullOrEmpty(replacement)))
        {
            read.Latest.Deprecated = true;
            read.Latest.Message = string.IsNullOrEmpty(replacement) ? "Abandoned" : string.Format(CultureInfo.InvariantCulture, "Abandoned; use {0}", replacement);
        }

        return read;
    }

    private static bool IsStableComposer(string version) => !version.Contains('-', StringComparison.Ordinal) && !version.Contains("dev", StringComparison.OrdinalIgnoreCase);

    #endregion

    #region pub.dev

    /// <summary>
    /// pub.dev (<c>/api/packages/&lt;name&gt;</c>): <c>name</c>, <c>latest.version</c>, <c>latest.pubspec.repository</c>
    /// (or <c>homepage</c>), <c>versions[]</c> with <c>version</c>, <c>published</c> and <c>retracted</c> (a retracted
    /// version reads as yanked); and, when given, the options (<c>/api/packages/&lt;name&gt;/options</c>):
    /// <c>isDiscontinued</c> and <c>replacedBy</c> as the deprecation of the latest version.
    /// </summary>
    public static RegistryPackageRead ParsePub(string json, string optionsJson)
    {
        var root = Parse(json);
        var pubspec = root?["latest"]?["pubspec"];
        var read = new RegistryPackageRead
        {
            Name = Text(root?["name"]),
            LatestVersion = Text(root?["latest"]?["version"]),
            Repository = GitUrl(Text(pubspec?["repository"])) ?? GitUrl(Text(pubspec?["homepage"])),
        };

        foreach (var item in (root?["versions"] as JsonArray ?? []).Where(v => v != null))
            read.Versions.Add(new RegistryVersionRead { Version = Text(item["version"]), Published = Date(item["published"]), Yanked = Bool(item["retracted"]) });

        var options = Parse(optionsJson);

        if (read.Latest != null && Bool(options?["isDiscontinued"]))
        {
            var replacedBy = Text(options["replacedBy"]);

            read.Latest.Deprecated = true;
            read.Latest.Message = string.IsNullOrEmpty(replacedBy) ? "Discontinued" : string.Format(CultureInfo.InvariantCulture, "Discontinued; use {0}", replacedBy);
        }

        return read;
    }

    #endregion

    private static string NonEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
