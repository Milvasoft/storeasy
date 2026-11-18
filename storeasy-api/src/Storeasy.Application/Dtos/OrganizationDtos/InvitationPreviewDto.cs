namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// What the invitation link shows before signing in (design 5.22 to 5.25). A revoked invitation reveals nothing but its
/// state; an accepted one only the organization; an expired one the organization and who invited, so a new invitation
/// can be asked for. The address is always masked.
/// </summary>
public class InvitationPreviewDto
{
    /// <summary>
    /// State of the invitation.
    /// </summary>
    public InvitationStatus Status { get; set; }

    /// <summary>
    /// Organization name (not for a revoked invitation).
    /// </summary>
    public string OrganizationName { get; set; }

    /// <summary>
    /// Organization slug (not for a revoked invitation).
    /// </summary>
    public string OrganizationSlug { get; set; }

    /// <summary>
    /// Who sent the invitation (pending and expired only).
    /// </summary>
    public string InviterName { get; set; }

    /// <summary>
    /// Role on acceptance (pending only).
    /// </summary>
    public OrgRole? Role { get; set; }

    /// <summary>
    /// Name of the custom role when <see cref="Role"/> is <see cref="OrgRole.Custom"/>.
    /// </summary>
    public string CustomRoleName { get; set; }

    /// <summary>
    /// What the role may do (pending only).
    /// </summary>
    public IReadOnlyList<OrgPermission> Permissions { get; set; }

    /// <summary>
    /// The invited address, masked (c**@example.com; pending and expired only).
    /// </summary>
    public string MaskedEmail { get; set; }

    /// <summary>
    /// UTC instant the invitation expires or expired (pending and expired only).
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
}
