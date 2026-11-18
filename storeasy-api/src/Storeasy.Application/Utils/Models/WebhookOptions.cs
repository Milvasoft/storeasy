namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Inbound webhook settings (configuration section <see cref="SectionName"/>).
/// </summary>
public class WebhookOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Webhooks";

    /// <summary>
    /// Largest accepted request body.
    /// </summary>
    public const int MaxBodyBytes = 256 * 1024;

    /// <summary>
    /// Public address the receivers are reached at (the web app's origin; nginx passes <c>/api</c> on). Defaults to
    /// <see cref="AppOptions.BaseUrl"/> when empty.
    /// </summary>
    public string PublicBaseUrl { get; set; }

    /// <summary>
    /// Hours the previous secret stays valid after a rotation.
    /// </summary>
    public int SecretGraceHours { get; set; } = 24;

    /// <summary>
    /// Oldest App Store Connect event (by its timestamp) still processed, in hours; older ones are kept as stale.
    /// </summary>
    public int MaxEventAgeHours { get; set; } = 168;

    /// <summary>
    /// Largest clock difference accepted for storeasy signed webhooks, in seconds.
    /// </summary>
    public int SignedToleranceSeconds { get; set; } = 300;

    /// <summary>
    /// The receiving address of an inbound endpoint: <c>{base}/api/webhooks/{apple|signed}/{publicId}</c>, under <c>/api</c>
    /// so the web container's proxy passes it to the API like every other call.
    /// </summary>
    /// <param name="appOptions"></param>
    /// <param name="source"></param>
    /// <param name="publicId"></param>
    /// <returns></returns>
    public string ReceiverUrl(AppOptions appOptions, WebhookSource source, string publicId)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(PublicBaseUrl) ? appOptions?.BaseUrl : PublicBaseUrl) ?? string.Empty;

        return $"{baseUrl.TrimEnd('/')}/api/webhooks/{(source == WebhookSource.AppStoreConnect ? "apple" : "signed")}/{publicId}";
    }
}
