namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// Result of an invitation. <see cref="Token"/> is shown only here; the app builds the accept link from it so the
/// inviter can share it when e-mail is not configured.
/// </summary>
public class InvitationCreatedDto
{
    /// <summary>
    /// Invitation id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// One time token for the accept link.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// UTC expiry.
    /// </summary>
    public DateTime ExpiryDate { get; set; }
}
