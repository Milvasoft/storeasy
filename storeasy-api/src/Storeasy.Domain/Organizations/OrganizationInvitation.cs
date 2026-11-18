using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Organizations;

/// <summary>
/// An e-mail invitation to join an organization with a role. Only the SHA-256 hash of the token is stored; the token
/// itself is shown once in the invitation link. An invitation is single use and expires after <see cref="Lifetime"/>.
/// </summary>
[Table(TableNames.OrganizationInvitations)]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(OrganizationId), nameof(NormalizedEmail))]
public class OrganizationInvitation : CreationAuditableEntity<int>
{
    /// <summary>
    /// How long an invitation stays valid.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// Organization identifier.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Invited e-mail address as typed.
    /// </summary>
    [MaxLength(256)]
    public string Email { get; set; }

    /// <summary>
    /// Upper case form of <see cref="Email"/> for lookups.
    /// </summary>
    [MaxLength(256)]
    public string NormalizedEmail { get; set; }

    /// <summary>
    /// Role the invitee gets on acceptance. Never <see cref="OrgRole.Owner"/>.
    /// </summary>
    public OrgRole Role { get; set; }

    /// <summary>
    /// Hex SHA-256 hash of the invitation token.
    /// </summary>
    [MaxLength(64)]
    public string TokenHash { get; set; }

    /// <summary>
    /// UTC instant after which the invitation can no longer be accepted.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// UTC instant of acceptance. Null while pending.
    /// </summary>
    public DateTime? AcceptDate { get; set; }

    /// <summary>
    /// UTC instant of revocation. Null unless an admin revoked it.
    /// </summary>
    public DateTime? RevokeDate { get; set; }

    /// <summary>
    /// Id of the user who sent the invitation.
    /// </summary>
    public int InviterUserId { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
