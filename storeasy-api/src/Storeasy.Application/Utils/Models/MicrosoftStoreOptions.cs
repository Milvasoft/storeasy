namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the Microsoft Store client (configuration section <see cref="SectionName"/>), bound once and registered
/// as a singleton. The defaults are the public Microsoft endpoints; tests and self-hosters normally change nothing.
/// </summary>
public class MicrosoftStoreOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:MicrosoftStore";

    /// <summary>
    /// Microsoft Entra authority; the token endpoint is <c>{LoginBaseUrl}/{tenantId}/oauth2/v2.0/token</c>.
    /// </summary>
    public string LoginBaseUrl { get; set; } = "https://login.microsoftonline.com";

    /// <summary>
    /// Base address of the MSIX submission API (apps, submissions, flights, add-ons).
    /// </summary>
    public string DevCenterBaseUrl { get; set; } = "https://manage.devcenter.microsoft.com/v1.0/my";

    /// <summary>
    /// Client credentials scope of the MSIX submission API.
    /// </summary>
    public string DevCenterScope { get; set; } = "https://manage.devcenter.microsoft.com/.default";

    /// <summary>
    /// Base address of the MSI/EXE submission API.
    /// </summary>
    public string StoreApiBaseUrl { get; set; } = "https://api.store.microsoft.com";

    /// <summary>
    /// Client credentials scope of the MSI/EXE submission API.
    /// </summary>
    public string StoreApiScope { get; set; } = "https://api.store.microsoft.com/.default";

    /// <summary>
    /// Partner Center page of a product, for assisted steps; <c>{productId}</c> is replaced.
    /// </summary>
    public string PartnerCenterProductUrl { get; set; } = "https://partner.microsoft.com/dashboard/products/{productId}/overview";

    /// <summary>
    /// Attempts of one call when Microsoft answers 429, 5xx or the network fails.
    /// </summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>
    /// Wait before the first retry, in milliseconds; doubled on every further attempt (a Retry-After answer wins).
    /// </summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Longest wait between two attempts, in seconds.
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Pause between two status reads after a commit or a package commit, in milliseconds.
    /// </summary>
    public int StatusPollIntervalMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Status reads after a commit before the job leaves the rest to the sync.
    /// </summary>
    public int StatusPollAttempts { get; set; } = 24;

    /// <summary>
    /// Largest installer the Authenticode check downloads, in bytes.
    /// </summary>
    public long InstallerCheckMaxBytes { get; set; } = 4L * 1024 * 1024 * 1024;

    /// <summary>
    /// Whether installer downloads refuse hosts that resolve to loopback, private or link local addresses (SSRF guard).
    /// </summary>
    public bool BlockPrivateDownloadAddresses { get; set; } = true;
}
