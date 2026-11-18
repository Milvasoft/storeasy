namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Web push settings (configuration section <see cref="SectionName"/>), bound once and registered as a singleton. Pushes
/// go straight from this server to the browsers' push services with VAPID (RFC 8292); no third party account is needed.
/// Off until <see cref="PublicKey"/> and <see cref="PrivateKey"/> are set (a P-256 key pair, see docs/self-host.md for the
/// generation command). Changing the key pair invalidates every existing browser subscription.
/// </summary>
public class WebPushOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:WebPush";

    /// <summary>
    /// Contact the push services may use (<c>mailto:</c> address or https page).
    /// </summary>
    public string Subject { get; set; } = "mailto:admin@localhost";

    /// <summary>
    /// VAPID public key: the uncompressed P-256 point (65 bytes), base64url. The browser subscribes with it.
    /// </summary>
    public string PublicKey { get; set; }

    /// <summary>
    /// VAPID private key: the P-256 private scalar (32 bytes), base64url (secret, set through the environment).
    /// </summary>
    public string PrivateKey { get; set; }

    /// <summary>
    /// How long a push service keeps an undelivered push.
    /// </summary>
    public int TimeToLiveSeconds { get; set; } = 86400;

    /// <summary>
    /// Whether web push is configured.
    /// </summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}
