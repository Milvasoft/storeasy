namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the package registries module (<c>Storeasy:Registries</c>). Only public, anonymous APIs are read; the tests
/// point the addresses at fake registries.
/// </summary>
public class RegistryOptions
{
    /// <summary>
    /// Configuration section.
    /// </summary>
    public const string SectionName = "Storeasy:Registries";

    /// <summary>
    /// npm registry (<c>GET {NpmBaseUrl}/&lt;name&gt;</c> and the attestations endpoint).
    /// </summary>
    public string NpmBaseUrl { get; set; } = "https://registry.npmjs.org";

    /// <summary>
    /// NuGet registration resource (<c>RegistrationsBaseUrl/3.6.0</c> of the nuget.org service index: gzip, SemVer 2.0.0).
    /// </summary>
    public string NuGetRegistrationBaseUrl { get; set; } = "https://api.nuget.org/v3/registration5-gz-semver2";

    /// <summary>
    /// PyPI (JSON API <c>/pypi/&lt;project&gt;/json</c> and Integrity API <c>/integrity/...</c>).
    /// </summary>
    public string PyPIBaseUrl { get; set; } = "https://pypi.org";

    /// <summary>
    /// crates.io API.
    /// </summary>
    public string CratesBaseUrl { get; set; } = "https://crates.io/api/v1";

    /// <summary>
    /// Maven Central repository (<c>maven-metadata.xml</c> of an artifact).
    /// </summary>
    public string MavenRepositoryBaseUrl { get; set; } = "https://repo1.maven.org/maven2";

    /// <summary>
    /// Maven Central search API (the publish dates of the versions).
    /// </summary>
    public string MavenSearchBaseUrl { get; set; } = "https://search.maven.org/solrsearch/select";

    /// <summary>
    /// RubyGems API.
    /// </summary>
    public string RubyGemsBaseUrl { get; set; } = "https://rubygems.org";

    /// <summary>
    /// Packagist metadata repository (<c>/p2/&lt;vendor&gt;/&lt;package&gt;.json</c>).
    /// </summary>
    public string PackagistBaseUrl { get; set; } = "https://repo.packagist.org";

    /// <summary>
    /// pub.dev API.
    /// </summary>
    public string PubBaseUrl { get; set; } = "https://pub.dev";

    /// <summary>
    /// NuGet search (<c>SearchQueryService</c> of the nuget.org service index): download counts and listing.
    /// </summary>
    public string NuGetSearchUrl { get; set; } = "https://azuresearch-usnc.nuget.org/query";

    /// <summary>
    /// NuGet flat container (<c>ReadmeUriTemplate</c> of the service index): package READMEs.
    /// </summary>
    public string NuGetFlatContainerBaseUrl { get; set; } = "https://api.nuget.org/v3-flatcontainer";

    /// <summary>
    /// npm download counts API.
    /// </summary>
    public string NpmDownloadsBaseUrl { get; set; } = "https://api.npmjs.org";

    /// <summary>
    /// pypistats.org API (PyPI download counts; PyPI itself publishes none).
    /// </summary>
    public string PyPIStatsBaseUrl { get; set; } = "https://pypistats.org/api";

    /// <summary>
    /// Packagist site (package details with download counts).
    /// </summary>
    public string PackagistSiteBaseUrl { get; set; } = "https://packagist.org";

    /// <summary>
    /// OSV.dev API (security advisories).
    /// </summary>
    public string OsvBaseUrl { get; set; } = "https://api.osv.dev/v1";

    /// <summary>
    /// User-Agent of every request (crates.io refuses requests without a descriptive one).
    /// </summary>
    public string UserAgent { get; set; } = "storeasy (+https://storeasy.milvasoft.com)";

    /// <summary>
    /// Seconds one registry request may take.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Hours after which the schedule syncs a package again.
    /// </summary>
    public int SyncIntervalHours { get; set; } = 6;

    /// <summary>
    /// Most versions kept per package (the newest).
    /// </summary>
    public int MaxVersions { get; set; } = 1000;

    /// <summary>
    /// Versions per package and sync whose provenance is read one by one (PyPI, where the version list does not say);
    /// the latest version is always read.
    /// </summary>
    public int ProvenanceChecksPerSync { get; set; } = 5;

    /// <summary>
    /// Most advisories read in detail per sync (OSV.dev's batch query returns only ids).
    /// </summary>
    public int MaxAdvisoryDetails { get; set; } = 50;

    /// <summary>
    /// Most packages an organization can track.
    /// </summary>
    public int MaxPackagesPerOrganization { get; set; } = 500;
}
