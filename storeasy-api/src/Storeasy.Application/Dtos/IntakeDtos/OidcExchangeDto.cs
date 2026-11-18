namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A 15 minute upload token issued for an OIDC token (tools/CONTRACT.md 3.1).
/// </summary>
public class OidcExchangeDto
{
    /// <summary>
    /// The upload token (sut_).
    /// </summary>
    public string UploadToken { get; set; }

    /// <summary>
    /// UTC expiry.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// The one app the token may upload to.
    /// </summary>
    public int AppId { get; set; }
}
