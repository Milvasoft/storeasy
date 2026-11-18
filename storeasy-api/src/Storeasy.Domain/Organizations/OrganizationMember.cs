using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Organizations;

/// <summary>
/// Membership of a user in an organization with one <see cref="OrgRole"/>. A user has at most one row per organization.
/// </summary>
[Table(TableNames.OrganizationMembers)]
[Index(nameof(OrganizationId), nameof(UserId), IsUnique = true)]
[Index(nameof(UserId))]
public class OrganizationMember : CreationAuditableEntity<int>
{
    /// <summary>
    /// Organization identifier.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Member user identifier.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Role of the user in this organization.
    /// </summary>
    public OrgRole Role { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }

    /// <summary>
    /// Navigation property of the user.
    /// </summary>
    public virtual User User { get; set; }
}
