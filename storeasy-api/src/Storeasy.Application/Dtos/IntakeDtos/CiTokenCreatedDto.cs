namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A new CI token. <see cref="Token"/> is shown only in this response.
/// </summary>
public class CiTokenCreatedDto
{
    /// <summary>
    /// Token id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The token value (shown once).
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Display prefix.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// UTC expiry.
    /// </summary>
    public DateTime ExpiryDate { get; set; }
}
