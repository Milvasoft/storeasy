namespace Storeasy.Domain.JsonModels;

/// <summary>
/// Non secret identifiers of a store connection, shown in place of the secret.
/// </summary>
public class ConnectionFingerprint
{
    /// <summary>
    /// App Store Connect key id (10 characters). Null for Google.
    /// </summary>
    public string KeyId { get; set; }

    /// <summary>
    /// Last 4 characters of the App Store Connect issuer id. Null for Google.
    /// </summary>
    public string IssuerIdSuffix { get; set; }

    /// <summary>
    /// Google service account e-mail. Null for Apple.
    /// </summary>
    public string ServiceAccountEmail { get; set; }

    /// <summary>
    /// Google Cloud project id of the service account. Null for Apple.
    /// </summary>
    public string ProjectId { get; set; }

    /// <summary>
    /// Last 4 characters of the Microsoft Entra tenant id. Microsoft only.
    /// </summary>
    public string TenantIdSuffix { get; set; }

    /// <summary>
    /// Last 4 characters of the Microsoft Entra application (client) id. Microsoft only.
    /// </summary>
    public string ClientIdSuffix { get; set; }

    /// <summary>
    /// Partner Center seller id (sent as <c>X-Seller-Account-Id</c>). Microsoft only.
    /// </summary>
    public string SellerId { get; set; }

    /// <summary>
    /// UTC instant the credential expires, as entered by the user (Microsoft client secrets, GitHub tokens).
    /// </summary>
    public DateTime? SecretExpiryDate { get; set; }

    /// <summary>
    /// Account the channel writes as, for channels that write through a code host (the GitHub user or organization that
    /// owns the winget-pkgs fork).
    /// </summary>
    public string AccountLogin { get; set; }

    /// <summary>
    /// Repository the channel writes to, as <c>owner/name</c> (the winget-pkgs fork).
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// Kind of token, told from its prefix (<c>classic</c>, <c>fine-grained</c>, <c>app</c>), never the token itself.
    /// </summary>
    public string TokenKind { get; set; }

    /// <summary>
    /// Publisher id of a channel that addresses its items under a publisher (Chrome Web Store).
    /// </summary>
    public string PublisherId { get; set; }
}