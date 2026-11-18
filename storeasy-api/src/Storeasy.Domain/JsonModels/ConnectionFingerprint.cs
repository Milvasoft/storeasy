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
}
