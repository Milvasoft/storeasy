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
    /// Issuers accepted on the OIDC token of a Pub/Sub push request (Google Play notifications).
    /// </summary>
    public List<string> GooglePushIssuers { get; set; } = ["https://accounts.google.com", "accounts.google.com"];

    /// <summary>
    /// Key set (JWKS) the Pub/Sub push tokens are signed with (https only).
    /// </summary>
    public string GooglePushKeysUrl { get; set; } = "https://www.googleapis.com/oauth2/v3/certs";

    /// <summary>
    /// The receiving address of an inbound endpoint: <c>{base}/api/webhooks/{apple|signed|google}/{publicId}</c>, under
    /// <c>/api</c> so the web container's proxy passes it to the API like every other call. For a Google Play endpoint it is
    /// also the audience the push token must carry.
    /// </summary>
    /// <param name="appOptions"></param>
    /// <param name="source"></param>
    /// <param name="publicId"></param>
    /// <returns></returns>
    public string ReceiverUrl(AppOptions appOptions, WebhookSource source, string publicId)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(PublicBaseUrl) ? appOptions?.BaseUrl : PublicBaseUrl) ?? string.Empty;

        var segment = source switch
        {
            WebhookSource.AppStoreConnect => "apple",
            WebhookSource.GooglePlay => "google",
            _ => "signed",
        };

        return $"{baseUrl.TrimEnd('/')}/api/webhooks/{segment}/{publicId}";
    }
}
