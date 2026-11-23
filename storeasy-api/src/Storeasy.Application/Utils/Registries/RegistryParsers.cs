using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// Reads the public JSON answers of npm, NuGet, PyPI, crates.io and OSV.dev into <see cref="RegistryPackageRead"/>,
/// <see cref="ProvenanceRead"/> and <see cref="RegistryAdvisory"/>. Field names follow the official documentation and the
/// live answers (docs/package-registries.md); unknown or missing fields are left empty, never guessed.
/// </summary>
public static partial class RegistryParsers
{
    /// <summary>
    /// SLSA provenance predicate types start with this (npm publishes v0.2 and v1).
    /// </summary>
    public const string SlsaPredicatePrefix = "https://slsa.dev/provenance/";

    private const int _maxSummaryLength = 300;

    #region npm

    /// <summary>
    /// The npm packument (<c>GET /&lt;name&gt;</c>, full document): <c>dist-tags.latest</c>, <c>time.&lt;version&gt;</c>,
    /// <c>versions.&lt;version&gt;.deprecated</c>, <c>versions.&lt;version&gt;.dist.attestations.provenance</c> and
    /// <c>repository</c>.
    /// </summary>
    public static RegistryPackageRead ParseNpm(string json)
    {
        var root = Parse(json);
        var time = root?["time"] as JsonObject;
        var versions = root?["versions"] as JsonObject;
        var read = new RegistryPackageRead
        {
            Name = Text(root?["name"]),
            LatestVersion = Text(root?["dist-tags"]?["latest"]),
            Repository = RepositoryUrl(root?["repository"]),
        };

        if (versions != null)
        {
            foreach (var (version, node) in versions)
            {
                var deprecated = Text(node?["deprecated"]);

                read.Versions.Add(new RegistryVersionRead
                {
                    Version = version,
                    Published = Date(time?[version]),
                    Deprecated = !string.IsNullOrEmpty(deprecated),
                    Message = string.IsNullOrEmpty(deprecated) ? null : deprecated,
                    HasProvenance = node?["dist"]?["attestations"]?["provenance"] != null,
                });
            }
        }

        if (read.Repository == null && read.LatestVersion != null)
            read.Repository = RepositoryUrl(versions?[read.LatestVersion]?["repository"]);

        return read;
    }

    /// <summary>
    /// The npm attestations endpoint (<c>GET /-/npm/v1/attestations/&lt;name&gt;@&lt;version&gt;</c>): the attestation whose
    /// <c>predicateType</c> is SLSA provenance, its <c>bundle.dsseEnvelope.payload</c> (base64 in-toto statement). v1 names
    /// <c>predicate.buildDefinition.externalParameters.workflow.repository</c> and <c>.path</c> and
    /// <c>predicate.runDetails.metadata.invocationId</c>; v0.2 <c>predicate.invocation.configSource.uri</c> and
    /// <c>.entryPoint</c> and <c>predicate.metadata.buildInvocationId</c>.
    /// </summary>
    public static ProvenanceRead ParseNpmProvenance(string json)
    {
        var attestations = Parse(json)?["attestations"] as JsonArray;
        var provenance = attestations?.FirstOrDefault(a => Text(a?["predicateType"])?.StartsWith(SlsaPredicatePrefix, StringComparison.Ordinal) == true);
        return ProvenanceOfPayload(Text(provenance?["bundle"]?["dsseEnvelope"]?["payload"]));
    }

    /// <summary>
    /// Reads a base64 in-toto statement with a SLSA provenance predicate (v1 or v0.2); null when it is not one.
    /// </summary>
    private static ProvenanceRead ProvenanceOfPayload(string payload)
    {
        if (string.IsNullOrEmpty(payload))
            return null;

        JsonNode statement;

        try
        {
            statement = Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        }
        catch (FormatException)
        {
            return null;
        }

        var predicate = statement?["predicate"];
        var workflow = predicate?["buildDefinition"]?["externalParameters"]?["workflow"];

        if (workflow != null)
            return new ProvenanceRead(GitUrl(Text(workflow["repository"])), Text(workflow["path"]), RunUrl(Text(predicate["runDetails"]?["metadata"]?["invocationId"])));

        var source = predicate?["invocation"]?["configSource"];

        if (source == null)
            return null;

        return new ProvenanceRead(GitUrl(Text(source["uri"])), Text(source["entryPoint"]), RunUrl(Text(predicate["metadata"]?["buildInvocationId"])));
    }

    #endregion

    #region NuGet

    /// <summary>
    /// A NuGet registration index (<c>GET {base}/&lt;id-lower&gt;/index.json</c>): its pages, each with its <c>@id</c> and,
    /// when nuget.org inlined them, its leaves.
    /// </summary>
    public static List<(string PageUrl, JsonArray Leaves)> ParseNuGetIndex(string json)
        => [.. (Parse(json)?["items"] as JsonArray ?? []).Select(page => (Text(page?["@id"]), page?["items"] as JsonArray))];

    /// <summary>
    /// The leaves of a fetched NuGet registration page (<c>GET {page @id}</c>).
    /// </summary>
    public static JsonArray ParseNuGetPage(string json) => Parse(json)?["items"] as JsonArray ?? [];

    /// <summary>
    /// The package from its registration leaves: <c>catalogEntry.id</c>, <c>version</c>, <c>published</c> (year 1900 on
    /// nuget.org for an unlisted version), <c>listed</c> (absent means listed), <c>deprecation.message</c> and
    /// <c>.reasons</c>, <c>projectUrl</c>. The latest version is the highest listed stable one (or the highest listed).
    /// </summary>
    public static RegistryPackageRead ParseNuGetLeaves(IEnumerable<JsonNode> leaves)
    {
        var read = new RegistryPackageRead();
        string projectUrl = null;

        foreach (var entry in leaves.Select(leaf => leaf?["catalogEntry"]).Where(entry => entry != null))
        {
            var deprecation = entry["deprecation"];
            var published = Date(entry["published"]);
            var listed = entry["listed"] is not JsonValue listedValue || !listedValue.TryGetValue<bool>(out var isListed) || isListed;

            read.Name = Text(entry["id"]) ?? read.Name;
            projectUrl = Text(entry["projectUrl"]) ?? projectUrl;
            read.Versions.Add(new RegistryVersionRead
            {
                Version = Text(entry["version"]),
                Published = published?.Year <= 1900 ? null : published,
                Unlisted = !listed,
                Deprecated = deprecation != null,
                Message = deprecation == null ? null : Text(deprecation["message"]) ?? string.Join(", ", (deprecation["reasons"] as JsonArray ?? []).Select(Text)),
                HasProvenance = false,
            });
        }

        var listedVersions = read.Versions.Where(v => !v.Unlisted && v.Version != null).ToList();

        read.LatestVersion = (listedVersions.LastOrDefault(v => !v.Version.Contains('-', StringComparison.Ordinal)) ?? listedVersions.LastOrDefault())?.Version;
        read.Repository = projectUrl;

        return read;
    }

    #endregion

    #region PyPI

    /// <summary>
    /// The PyPI JSON API (<c>GET /pypi/&lt;project&gt;/json</c>): <c>info.name</c>, <c>info.version</c>,
    /// <c>info.project_urls</c>, <c>releases.&lt;version&gt;[]</c> with <c>filename</c>, <c>upload_time_iso_8601</c>,
    /// <c>yanked</c> and <c>yanked_reason</c>. A release is yanked when all its files are; it is published when its first file
    /// was uploaded.
    /// </summary>
    public static RegistryPackageRead ParsePyPI(string json)
    {
        var root = Parse(json);
        var info = root?["info"];
        var read = new RegistryPackageRead
        {
            Name = Text(info?["name"]),
            LatestVersion = Text(info?["version"]),
            Repository = PyPIRepository(info?["project_urls"] as JsonObject) ?? Text(info?["home_page"]),
        };

        if (root?["releases"] is JsonObject releases)
        {
            foreach (var (version, node) in releases)
            {
                var files = (node as JsonArray ?? []).Where(f => f != null).ToList();
                var yanked = files.Count > 0 && files.All(f => Bool(f["yanked"]));

                read.Versions.Add(new RegistryVersionRead
                {
                    Version = version,
                    Published = files.Select(f => Date(f["upload_time_iso_8601"])).Where(d => d != null).Min(),
                    Yanked = yanked,
                    Message = yanked ? files.Select(f => Text(f["yanked_reason"])).FirstOrDefault(r => !string.IsNullOrEmpty(r)) : null,
                    ArtifactFile = files.Select(f => Text(f["filename"])).FirstOrDefault(f => !string.IsNullOrEmpty(f)),
                });
            }
        }

        return read;
    }

    /// <summary>
    /// The PyPI Integrity API (<c>GET /integrity/&lt;project&gt;/&lt;version&gt;/&lt;file&gt;/provenance</c>):
    /// <c>attestation_bundles[].publisher</c> with <c>kind</c>, <c>repository</c> and <c>workflow</c>. PyPI names no CI run.
    /// </summary>
    public static ProvenanceRead ParsePyPIProvenance(string json)
    {
        var publisher = (Parse(json)?["attestation_bundles"] as JsonArray)?.Select(b => b?["publisher"]).FirstOrDefault(p => p != null);

        if (publisher == null)
            return null;

        var repository = Text(publisher["repository"]);
        var host = Text(publisher["kind"])?.ToLowerInvariant() switch
        {
            "github" => "https://github.com/",
            "gitlab" => "https://gitlab.com/",
            _ => null,
        };

        return new ProvenanceRead(host != null && repository != null ? host + repository : repository, Text(publisher["workflow"]), null);
    }

    #endregion

    #region crates.io

    /// <summary>
    /// crates.io (<c>GET /crates/&lt;name&gt;</c>): <c>crate.name</c>, <c>crate.max_stable_version</c> (or
    /// <c>max_version</c>), <c>crate.repository</c> and <c>versions[]</c> with <c>num</c>, <c>created_at</c>, <c>yanked</c>,
    /// <c>yank_message</c> and <c>trustpub_data</c> (trusted publishing: <c>provider</c> <c>github</c> with
    /// <c>repository</c> and <c>run_id</c>, or <c>gitlab</c> with <c>project_path</c> and <c>job_id</c>).
    /// </summary>
    public static RegistryPackageRead ParseCrate(string json)
    {
        var root = Parse(json);
        var crate = root?["crate"];
        var maxStable = Text(crate?["max_stable_version"]);
        var read = new RegistryPackageRead
        {
            Name = Text(crate?["name"]),
            LatestVersion = string.IsNullOrEmpty(maxStable) ? Text(crate?["max_version"]) : maxStable,
            Repository = Text(crate?["repository"]),
        };

        foreach (var node in (root?["versions"] as JsonArray ?? []).Where(v => v != null))
        {
            var trustpub = node["trustpub_data"] as JsonObject;
            var (repository, runUrl) = TrustedPublisher(trustpub);

            read.Versions.Add(new RegistryVersionRead
            {
                Version = Text(node["num"]),
                Published = Date(node["created_at"]),
                Yanked = Bool(node["yanked"]),
                Message = Text(node["yank_message"]),
                HasProvenance = trustpub != null,
                CiRunUrl = runUrl,
                SourceRepository = repository,
            });
        }

        return read;
    }

    private static (string Repository, string RunUrl) TrustedPublisher(JsonObject trustpub)
    {
        switch (Text(trustpub?["provider"]))
        {
            case "github":
                var repository = Text(trustpub["repository"]);
                var runId = Text(trustpub["run_id"]);

                return repository == null ? (null, null) : ($"https://github.com/{repository}", runId == null ? null : $"https://github.com/{repository}/actions/runs/{runId}");
            case "gitlab":
                var project = Text(trustpub["project_path"]);
                var jobId = Text(trustpub["job_id"]);

                return project == null ? (null, null) : ($"https://gitlab.com/{project}", jobId == null ? null : $"https://gitlab.com/{project}/-/jobs/{jobId}");
            default:
                return (null, null);
        }
    }

    #endregion

    #region OSV.dev

    /// <summary>
    /// The advisory ids of each query of an OSV.dev batch answer (<c>results[].vulns[].id</c>), in the query order.
    /// </summary>
    public static List<List<string>> ParseOsvBatch(string json)
        => [.. (Parse(json)?["results"] as JsonArray ?? []).Select(r => (r?["vulns"] as JsonArray ?? []).Select(v => Text(v?["id"])).Where(id => !string.IsNullOrEmpty(id)).ToList())];

    /// <summary>
    /// One OSV.dev advisory (<c>GET /vulns/&lt;id&gt;</c>): <c>id</c>, <c>summary</c> (or the first line of <c>details</c>),
    /// the severity word of <c>database_specific.severity</c> (GitHub advisories) or
    /// <c>affected[].ecosystem_specific.severity</c>; <c>withdrawn</c> advisories are skipped (null).
    /// </summary>
    public static RegistryAdvisory ParseOsvVulnerability(string json)
    {
        var root = Parse(json);
        var id = Text(root?["id"]);

        if (string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(Text(root["withdrawn"])))
            return null;

        var severity = Text(root["database_specific"]?["severity"])
                       ?? (root["affected"] as JsonArray ?? []).Select(a => Text(a?["ecosystem_specific"]?["severity"])).FirstOrDefault(s => !string.IsNullOrEmpty(s));
        var summary = Text(root["summary"]);

        if (string.IsNullOrWhiteSpace(summary))
            summary = Text(root["details"])?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

        if (summary?.Length > _maxSummaryLength)
            summary = summary[.._maxSummaryLength];

        return new RegistryAdvisory(id, severity?.ToUpperInvariant(), summary, RegistryNames.AdvisoryUrl(id));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A repository address without the <c>git+</c> prefix and the <c>.git</c> suffix (<c>git+https://github.com/o/r.git</c>
    /// is <c>https://github.com/o/r</c>); <c>git://</c> and <c>ssh://git@</c> become https; a ref after <c>@</c> is dropped.
    /// </summary>
    public static string GitUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();

        if (url.StartsWith("git+", StringComparison.OrdinalIgnoreCase))
            url = url[4..];

        if (url.StartsWith("git://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url[6..];
        else if (url.StartsWith("ssh://git@", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url[10..];

        var at = url.IndexOf('@', url.IndexOf("://", StringComparison.Ordinal) + 3);

        if (at > 0)
            url = url[..at];

        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];

        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? url : null;
    }

    /// <summary>
    /// A CI run address without the trailing <c>/attempts/&lt;n&gt;</c> of GitHub's invocation ids.
    /// </summary>
    public static string RunUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return null;

        var attempts = url.IndexOf("/attempts/", StringComparison.Ordinal);

        return attempts > 0 ? url[..attempts] : url;
    }

    private static string RepositoryUrl(JsonNode node) => node switch
    {
        JsonValue value => GitUrl(Text(value)),
        JsonObject obj => GitUrl(Text(obj["url"])),
        _ => null,
    };

    private static string PyPIRepository(JsonObject projectUrls)
    {
        if (projectUrls == null)
            return null;

        string[] preferred = ["source", "source code", "repository", "code", "github", "gitlab", "homepage", "home"];

        return preferred.Select(key => projectUrls.FirstOrDefault(p => string.Equals(p.Key.Trim(), key, StringComparison.OrdinalIgnoreCase)).Value)
                        .Select(Text)
                        .Select(GitUrl)
                        .FirstOrDefault(url => url != null);
    }

    private static JsonNode Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonNode node) => node is JsonValue value && value.GetValueKind() is JsonValueKind.String or JsonValueKind.Number
        ? (value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString())
        : null;

    private static bool Bool(JsonNode node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.True;

    private static DateTime? Date(JsonNode node)
        => DateTimeOffset.TryParse(Text(node), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date.UtcDateTime : null;

    #endregion
}
