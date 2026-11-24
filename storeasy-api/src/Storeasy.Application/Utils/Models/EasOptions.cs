namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of EAS builds through the Expo API (<c>Storeasy:Eas</c>). The tests point the address at a fake Expo.
/// </summary>
public class EasOptions
{
    /// <summary>
    /// Configuration section.
    /// </summary>
    public const string SectionName = "Storeasy:Eas";

    /// <summary>
    /// Expo GraphQL API (the endpoint eas-cli uses).
    /// </summary>
    public string ApiUrl { get; set; } = "https://api.expo.dev/graphql";

    /// <summary>
    /// Expo web site (build pages and the GitHub settings of a project).
    /// </summary>
    public string WebsiteUrl { get; set; } = "https://expo.dev";

    /// <summary>
    /// Hosts a finished build's package may be downloaded from when it is sent to the build intake.
    /// </summary>
    public List<string> DownloadDomains { get; set; } = ["expo.dev", "*.expo.dev", "storage.googleapis.com"];

    /// <summary>
    /// User-Agent of every request.
    /// </summary>
    public string UserAgent { get; set; } = "storeasy (+https://storeasy.milvasoft.com)";

    /// <summary>
    /// Seconds one request may take.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Recent builds read per project and sync.
    /// </summary>
    public int BuildsPerSync { get; set; } = 20;

    /// <summary>
    /// Hours storeasy waits for Expo to create a build it asked for before it gives up.
    /// </summary>
    public int RequestTimeoutHours { get; set; } = 2;
}
