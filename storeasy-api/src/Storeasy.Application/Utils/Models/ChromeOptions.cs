namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the Chrome Web Store channel (<c>Storeasy:Chrome</c>). The tests point the addresses at a fake server.
/// </summary>
public class ChromeOptions
{
    /// <summary>
    /// Configuration section.
    /// </summary>
    public const string SectionName = "Storeasy:Chrome";

    /// <summary>
    /// Chrome Web Store API base address (v2).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://chromewebstore.googleapis.com";

    /// <summary>
    /// Google OAuth token endpoint of the OAuth client credential (a service account key names its own).
    /// </summary>
    public string TokenUrl { get; set; } = "https://oauth2.googleapis.com/token";

    /// <summary>
    /// Pages of 200 reviews a review sync reads.
    /// </summary>
    public int ReviewPages { get; set; } = 5;

    /// <summary>
    /// Days the first metrics read goes back.
    /// </summary>
    public int MetricsBackfillDays { get; set; } = 90;

    /// <summary>
    /// Status reads after an upload the store is still processing, before the apply job gives the processing more time.
    /// </summary>
    public int UploadPollAttempts { get; set; } = 10;

    /// <summary>
    /// Wait between those reads, in milliseconds.
    /// </summary>
    public int UploadPollIntervalMilliseconds { get; set; } = 3000;
}
