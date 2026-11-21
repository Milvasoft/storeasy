namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of ASO keyword research and rank tracking (configuration section <see cref="SectionName"/>). Every source is a
/// public, unauthenticated store endpoint, called politely: one request at a time per host with
/// <see cref="MinRequestIntervalMilliseconds"/> between them, a timeout, a cache and a User-Agent without personal data.
/// </summary>
public class AsoOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Aso";

    /// <summary>
    /// Default User-Agent of the store requests: the product and its source, no person or organization.
    /// </summary>
    public const string DefaultUserAgent = "storeasy-aso/1.0 (+https://github.com/Milvasoft/storeasy)";

    /// <summary>
    /// Most active tracked keywords per organization (every store record and country together).
    /// </summary>
    public int MaxTrackedKeywordsPerOrganization { get; set; } = 200;

    /// <summary>
    /// Most keywords one research call measures.
    /// </summary>
    public int MaxResearchKeywords { get; set; } = 20;

    /// <summary>
    /// Timeout of one store request in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Pause between two requests to the same host in milliseconds (Apple asks for about 20 calls a minute).
    /// </summary>
    public int MinRequestIntervalMilliseconds { get; set; } = 3000;

    /// <summary>
    /// How long a store answer is reused from memory, and how old a saved measurement may be before research measures
    /// again, in hours.
    /// </summary>
    public int FreshnessHours { get; set; } = 24;

    /// <summary>
    /// How many search results are read for difficulty and rank (the iTunes Search API returns at most 200).
    /// </summary>
    public int RankDepth { get; set; } = 200;

    /// <summary>
    /// Most autocomplete requests per keyword when its popularity is measured (the full keyword counts as one).
    /// </summary>
    public int MaxPrefixQueries { get; set; } = 5;

    /// <summary>
    /// Most tracked keywords one run of the rank job checks; the rest are checked by the next runs of the same day.
    /// </summary>
    public int MaxRankChecksPerRun { get; set; } = 300;

    /// <summary>
    /// User-Agent of the store requests.
    /// </summary>
    public string UserAgent { get; set; } = DefaultUserAgent;

    /// <summary>
    /// Whether Google Play search suggestions are read for related terms and popularity (a public autocomplete endpoint).
    /// </summary>
    public bool GooglePlaySuggestionsEnabled { get; set; } = true;

    /// <summary>
    /// Whether Google Play search result pages are read for competitors and ranks. Off by default: Google Play has no
    /// search API, so this reads the public web page, which Google's terms of service may not allow for automated access
    /// and which can change without notice. The operator turns it on knowingly.
    /// </summary>
    public bool GooglePlayRanksEnabled { get; set; }

    /// <summary>
    /// Optional Apple Search Ads credentials for keyword popularity from Apple itself. Empty means not used.
    /// </summary>
    public AsoSearchAdsOptions SearchAds { get; set; } = new();
}

/// <summary>
/// Apple Search Ads Campaign Management API credentials (OAuth client credentials with a signed client secret). When
/// <see cref="IsConfigured"/> is false, popularity comes from the public search suggestions only.
/// </summary>
public class AsoSearchAdsOptions
{
    /// <summary>
    /// Search Ads API client id.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Search Ads API team id.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Key id of the uploaded public key.
    /// </summary>
    public string KeyId { get; set; }

    /// <summary>
    /// PEM private key (from the environment or user secrets, never committed).
    /// </summary>
    public string PrivateKey { get; set; }

    /// <summary>
    /// Search Ads organization id (the <c>X-AP-Context</c> orgId).
    /// </summary>
    public string OrgId { get; set; }

    /// <summary>
    /// Whether every credential is set.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId)
                                && !string.IsNullOrWhiteSpace(TeamId)
                                && !string.IsNullOrWhiteSpace(KeyId)
                                && !string.IsNullOrWhiteSpace(PrivateKey)
                                && !string.IsNullOrWhiteSpace(OrgId);
}
