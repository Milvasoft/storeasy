namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the Firefox Add-ons channel (<c>Storeasy:Firefox</c>). The tests point the address at a fake AMO.
/// </summary>
public class FirefoxOptions
{
    /// <summary>
    /// Configuration section.
    /// </summary>
    public const string SectionName = "Storeasy:Firefox";

    /// <summary>
    /// AMO API v5 base address.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://addons.mozilla.org/api/v5";

    /// <summary>
    /// Seconds a signed request token lives (AMO allows at most 300).
    /// </summary>
    public int TokenLifetimeSeconds { get; set; } = 60;

    /// <summary>
    /// Pages of 50 ratings a review sync reads.
    /// </summary>
    public int RatingPages { get; set; } = 5;

    /// <summary>
    /// Pages of 50 versions a sync reads.
    /// </summary>
    public int VersionPages { get; set; } = 2;

    /// <summary>
    /// Reads of an upload's validation before the apply job gives AMO more time (the job is retried).
    /// </summary>
    public int UploadPollAttempts { get; set; } = 20;

    /// <summary>
    /// Wait between those reads, in milliseconds.
    /// </summary>
    public int UploadPollIntervalMilliseconds { get; set; } = 3000;
}
