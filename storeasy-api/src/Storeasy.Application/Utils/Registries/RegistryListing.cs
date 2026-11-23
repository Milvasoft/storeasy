using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// A package's page on its registry as the registry serves it.
/// </summary>
public sealed class RegistryListingRead
{
    /// <summary>Version the listing belongs to.</summary>
    public string Version { get; set; }

    /// <summary>Short description (summary).</summary>
    public string Description { get; set; }

    /// <summary>README text; null when the registry has none for the package.</summary>
    public string Readme { get; set; }

    /// <summary>Format the registry served the README in.</summary>
    public ReadmeFormat? ReadmeFormat { get; set; }

    /// <summary>Keywords, tags or topics.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>License (SPDX expression or name, or a license address).</summary>
    public string License { get; set; }

    /// <summary>Project home page.</summary>
    public string Homepage { get; set; }

    /// <summary>Source repository.</summary>
    public string Repository { get; set; }

    /// <summary>Authors.</summary>
    public List<string> Authors { get; set; } = [];

    /// <summary>Icon address (NuGet).</summary>
    public string IconUrl { get; set; }
}

/// <summary>
/// One readiness check of a listing.
/// </summary>
/// <param name="Key">What is checked: <c>description</c>, <c>readme</c>, <c>license</c>, <c>repository</c>,
/// <c>keywords</c> or <c>icon</c>.</param>
/// <param name="Status">Result.</param>
public sealed record ListingCheck(string Key, ListingCheckStatus Status);

/// <summary>
/// Reads registry listings and checks them (docs/package-registries.md). No registry offers an API that edits this
/// metadata: it ships with the next release (npm, NuGet, PyPI, crates.io, RubyGems, pub.dev, Maven Central) or comes from
/// the repository (Packagist reads <c>composer.json</c>).
/// </summary>
public static partial class RegistryListing
{
    /// <summary>
    /// A description shorter than this is flagged.
    /// </summary>
    public const int ShortDescription = 20;

    /// <summary>
    /// Longest README kept (characters).
    /// </summary>
    public const int MaxReadmeLength = 100_000;

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex("<br\\s*/?>|</(p|div|h[1-6]|li|tr|pre)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex("\\n{3,}")]
    private static partial Regex BlankLines();

    /// <summary>
    /// Whether the registry serves a README storeasy can read (npm, NuGet, PyPI, crates.io).
    /// </summary>
    public static bool HasReadme(PackageRegistry registry) => registry is PackageRegistry.Npm or PackageRegistry.NuGet or PackageRegistry.PyPI or PackageRegistry.Crates;

    /// <summary>
    /// Whether the listing changes only with a new release (every registry but Packagist, which reads the repository).
    /// </summary>
    public static bool ShipsWithRelease(PackageRegistry registry) => registry != PackageRegistry.Packagist;

    /// <summary>
    /// npm packument: <c>description</c>, <c>readme</c> (Markdown), <c>keywords</c>, <c>license</c>, <c>homepage</c>,
    /// <c>repository</c>, <c>author</c> and <c>maintainers</c>.
    /// </summary>
    public static RegistryListingRead ParseNpm(string json)
    {
        var root = RegistryParsers.Parse(json);

        return new RegistryListingRead
        {
            Version = Text(root?["dist-tags"]?["latest"]),
            Description = Text(root?["description"]),
            Readme = Readme(Text(root?["readme"])),
            ReadmeFormat = string.IsNullOrWhiteSpace(Text(root?["readme"])) ? null : Domain.Enums.ReadmeFormat.Markdown,
            Keywords = Strings(root?["keywords"]),
            License = Text(root?["license"]) ?? Text(root?["license"]?["type"]),
            Homepage = Text(root?["homepage"]),
            Repository = RegistryParsers.GitUrl(Text(root?["repository"]?["url"]) ?? Text(root?["repository"])),
            Authors = [.. new[] { Text(root?["author"]?["name"]) ?? Text(root?["author"]) }.Concat((root?["maintainers"] as JsonArray ?? []).Select(m => Text(m?["name"]))).Where(a => !string.IsNullOrEmpty(a)).Distinct()],
        };
    }

    /// <summary>
    /// NuGet: the search entry (<c>description</c>, <c>tags</c>, <c>iconUrl</c>, <c>licenseUrl</c>, <c>projectUrl</c>,
    /// <c>authors</c>, <c>version</c>) and the README of the flat container (<c>{lower id}/{lower version}/readme</c>).
    /// </summary>
    public static RegistryListingRead ParseNuGet(string searchJson, string id, string readme)
    {
        var item = (RegistryParsers.Parse(searchJson)?["data"] as JsonArray ?? []).FirstOrDefault(d => string.Equals(Text(d?["id"]), id, StringComparison.OrdinalIgnoreCase));

        return new RegistryListingRead
        {
            Version = Text(item?["version"]),
            Description = Text(item?["description"]),
            Readme = Readme(readme),
            ReadmeFormat = string.IsNullOrWhiteSpace(readme) ? null : Domain.Enums.ReadmeFormat.Markdown,
            Keywords = [.. Strings(item?["tags"]).Where(t => !string.IsNullOrWhiteSpace(t))],
            License = Text(item?["licenseUrl"]),
            Homepage = Text(item?["projectUrl"]),
            Repository = RegistryParsers.GitUrl(Text(item?["projectUrl"]))?.Contains("github.com", StringComparison.OrdinalIgnoreCase) == true ? Text(item?["projectUrl"]) : null,
            Authors = Strings(item?["authors"]),
            IconUrl = Text(item?["iconUrl"]),
        };
    }

    /// <summary>
    /// PyPI JSON <c>info</c>: <c>summary</c>, <c>description</c> (the README) with <c>description_content_type</c>,
    /// <c>keywords</c> (comma or space separated), <c>license_expression</c> or <c>license</c>, <c>project_urls</c>,
    /// <c>author</c>.
    /// </summary>
    public static RegistryListingRead ParsePyPI(string json)
    {
        var info = RegistryParsers.Parse(json)?["info"];
        var urls = info?["project_urls"] as JsonObject;
        var readme = Text(info?["description"]);
        var keywords = Text(info?["keywords"]);

        return new RegistryListingRead
        {
            Version = Text(info?["version"]),
            Description = Text(info?["summary"]),
            Readme = Readme(readme),
            ReadmeFormat = string.IsNullOrWhiteSpace(readme) ? null : Text(info?["description_content_type"])?.StartsWith("text/markdown", StringComparison.OrdinalIgnoreCase) == true ? Domain.Enums.ReadmeFormat.Markdown : Domain.Enums.ReadmeFormat.Text,
            Keywords = string.IsNullOrWhiteSpace(keywords) ? [] : [.. keywords.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            License = Text(info?["license_expression"]) ?? Text(info?["license"]),
            Homepage = Url(urls, "homepage", "home") ?? Text(info?["home_page"]),
            Repository = RegistryParsers.GitUrl(Url(urls, "source", "source code", "repository", "code")),
            Authors = [.. new[] { Text(info?["author"]), Text(info?["maintainer"]) }.Where(a => !string.IsNullOrEmpty(a)).Distinct()],
        };
    }

    /// <summary>
    /// crates.io crate (<c>description</c>, <c>keywords</c>, <c>homepage</c>, <c>repository</c>, <c>max_stable_version</c>,
    /// the latest version's <c>license</c> and <c>published_by</c>) and its README, which crates.io serves as HTML.
    /// </summary>
    public static RegistryListingRead ParseCrate(string json, string readmeHtml)
    {
        var root = RegistryParsers.Parse(json);
        var crate = root?["crate"];
        var version = Text(crate?["max_stable_version"]) ?? Text(crate?["max_version"]);
        var latest = (root?["versions"] as JsonArray ?? []).FirstOrDefault(v => Text(v?["num"]) == version);
        var text = HtmlToText(readmeHtml);

        return new RegistryListingRead
        {
            Version = version,
            Description = Text(crate?["description"]),
            Readme = Readme(text),
            ReadmeFormat = string.IsNullOrWhiteSpace(text) ? null : Domain.Enums.ReadmeFormat.Html,
            Keywords = Strings(crate?["keywords"]),
            License = Text(latest?["license"]),
            Homepage = Text(crate?["homepage"]),
            Repository = Text(crate?["repository"]),
            Authors = [.. new[] { Text(latest?["published_by"]?["login"]) }.Where(a => !string.IsNullOrEmpty(a))],
        };
    }

    /// <summary>
    /// RubyGems gem: <c>info</c>, <c>licenses</c>, <c>homepage_uri</c>, <c>source_code_uri</c>, <c>authors</c> (one
    /// comma separated text). No README.
    /// </summary>
    public static RegistryListingRead ParseRubyGems(string json)
    {
        var gem = RegistryParsers.Parse(json);

        return new RegistryListingRead
        {
            Version = Text(gem?["version"]),
            Description = Text(gem?["info"]),
            License = string.Join(", ", Strings(gem?["licenses"])) is { Length: > 0 } licenses ? licenses : null,
            Homepage = Text(gem?["homepage_uri"]),
            Repository = Text(gem?["source_code_uri"]) ?? Text(gem?["metadata"]?["source_code_uri"]),
            Authors = [.. (Text(gem?["authors"]) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        };
    }

    /// <summary>
    /// Packagist package (<c>packagist.org/packages/&lt;name&gt;.json</c>): <c>description</c>, <c>repository</c>, and the
    /// newest stable version's <c>keywords</c>, <c>license</c>, <c>homepage</c> and <c>authors[].name</c>.
    /// </summary>
    public static RegistryListingRead ParsePackagist(string json, string latestVersion)
    {
        var package = RegistryParsers.Parse(json)?["package"];
        var version = latestVersion == null ? null : package?["versions"]?[latestVersion];

        return new RegistryListingRead
        {
            Version = latestVersion,
            Description = Text(package?["description"]),
            Keywords = Strings(version?["keywords"]),
            License = string.Join(", ", Strings(version?["license"])) is { Length: > 0 } license ? license : null,
            Homepage = Text(version?["homepage"]),
            Repository = Text(package?["repository"]),
            Authors = [.. (version?["authors"] as JsonArray ?? []).Select(a => Text(a?["name"])).Where(a => !string.IsNullOrEmpty(a))],
        };
    }

    /// <summary>
    /// pub.dev: the latest pubspec's <c>description</c>, <c>topics</c>, <c>homepage</c> and <c>repository</c>; the license
    /// from the score tags (<c>license:&lt;id&gt;</c>). No README through the API.
    /// </summary>
    public static RegistryListingRead ParsePub(string json, string scoreJson)
    {
        var latest = RegistryParsers.Parse(json)?["latest"];
        var pubspec = latest?["pubspec"];
        var license = Strings(RegistryParsers.Parse(scoreJson)?["tags"]).FirstOrDefault(t => t.StartsWith("license:", StringComparison.Ordinal));

        return new RegistryListingRead
        {
            Version = Text(latest?["version"]),
            Description = Text(pubspec?["description"]),
            Keywords = Strings(pubspec?["topics"]),
            License = license?["license:".Length..],
            Homepage = Text(pubspec?["homepage"]),
            Repository = Text(pubspec?["repository"]),
        };
    }

    /// <summary>
    /// The readiness checks of a listing: description (missing, or shorter than <see cref="ShortDescription"/>), README
    /// (where the registry serves one), license, repository, keywords and, on NuGet, the icon.
    /// </summary>
    public static List<ListingCheck> Checks(PackageRegistry registry, RegistryListingRead listing)
    {
        var description = listing.Description?.Trim();
        var checks = new List<ListingCheck>
        {
            new("description", string.IsNullOrEmpty(description) ? ListingCheckStatus.Missing : description.Length < ShortDescription ? ListingCheckStatus.Warning : ListingCheckStatus.Ok),
        };

        if (HasReadme(registry))
            checks.Add(new ListingCheck("readme", string.IsNullOrWhiteSpace(listing.Readme) ? ListingCheckStatus.Missing : ListingCheckStatus.Ok));

        checks.Add(new ListingCheck("license", string.IsNullOrWhiteSpace(listing.License) ? ListingCheckStatus.Missing : ListingCheckStatus.Ok));
        checks.Add(new ListingCheck("repository", string.IsNullOrWhiteSpace(listing.Repository) ? ListingCheckStatus.Missing : ListingCheckStatus.Ok));
        checks.Add(new ListingCheck("keywords", listing.Keywords.Count == 0 ? ListingCheckStatus.Warning : ListingCheckStatus.Ok));

        if (registry == PackageRegistry.NuGet)
            checks.Add(new ListingCheck("icon", string.IsNullOrWhiteSpace(listing.IconUrl) ? ListingCheckStatus.Warning : ListingCheckStatus.Ok));

        return checks;
    }

    /// <summary>
    /// The text of an HTML README: scripts and styles dropped, block ends as line breaks, tags removed, entities decoded.
    /// The web app shows README text only, never registry HTML.
    /// </summary>
    public static string HtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var text = ScriptOrStyle().Replace(html, string.Empty);

        text = BlockEnd().Replace(text, "\n");
        text = WebUtility.HtmlDecode(Tag().Replace(text, string.Empty));

        return BlankLines().Replace(text.Replace("\r\n", "\n", StringComparison.Ordinal), "\n\n").Trim();
    }

    private static string Readme(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Length > MaxReadmeLength ? text[..MaxReadmeLength] : text;
    }

    private static string Url(JsonObject urls, params string[] keys)
        => urls == null ? null : keys.Select(key => urls.FirstOrDefault(p => string.Equals(p.Key.Trim(), key, StringComparison.OrdinalIgnoreCase)).Value).Select(Text).FirstOrDefault(u => !string.IsNullOrEmpty(u));

    private static List<string> Strings(JsonNode node) => node switch
    {
        JsonArray array => [.. array.Select(Text).Where(s => !string.IsNullOrEmpty(s))],
        JsonValue => Text(node) is string single ? [single] : [],
        _ => [],
    };

    private static string Text(JsonNode node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
