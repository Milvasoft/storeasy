namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A Microsoft Entra connection whose client secret expires soon (for reminders).
/// </summary>
public class MsExpiringConnectionDto
{
    /// <summary>
    /// Connection id.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Connection name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// UTC instant the secret expires.
    /// </summary>
    public DateTime CredentialExpiryDate { get; set; }

    /// <summary>
    /// Whole days left (negative when expired).
    /// </summary>
    public int DaysLeft { get; set; }
}
