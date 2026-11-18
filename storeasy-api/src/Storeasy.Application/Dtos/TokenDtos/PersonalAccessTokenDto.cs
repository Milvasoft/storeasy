namespace Storeasy.Application.Dtos.TokenDtos;

/// <summary>
/// One of the caller's personal access tokens (design 16.11).
/// </summary>
public class PersonalAccessTokenDto
{
    /// <summary>
    /// Token id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Organization name.
    /// </summary>
    public string OrganizationName { get; set; }

    /// <summary>
    /// Whether the token only reads.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Display prefix.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// UTC expiry.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// UTC instant of the last use.
    /// </summary>
    public DateTime? LastUseDate { get; set; }

    /// <summary>
    /// Client of the last use.
    /// </summary>
    public string LastClientName { get; set; }

    /// <summary>
    /// UTC creation instant.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Whether the token has expired.
    /// </summary>
    public bool IsExpired { get; set; }
}
