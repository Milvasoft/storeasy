using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Organizations;

/// <summary>
/// A role an organization defined for itself (docs/urun-plani.md 2.1, phase 8): a name and a set of
/// <see cref="OrgPermission"/>s. Members and invitations hold it with <see cref="OrgRole.Custom"/>. The seeded roles
/// (owner, admin, editor, viewer) are not rows: they stay locked in <see cref="OrgRoleRules"/>. A custom role never holds
/// the owner only permission (<see cref="OrgPermission.ManageOrganization"/>) and always holds
/// <see cref="OrgPermission.View"/>. A role in use by a member or a pending invitation cannot be deleted.
/// </summary>
[Table(TableNames.OrganizationRoles)]
[Index(nameof(OrganizationId), nameof(Name), IsUnique = true)]
public class OrganizationRole : AuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>; the validators use the same limit.
    /// </summary>
    public const int NameMaxLength = 50;

    /// <summary>
    /// Column length of <see cref="Description"/>; the validators use the same limit.
    /// </summary>
    public const int DescriptionMaxLength = 200;

    /// <summary>
    /// How many custom roles one organization may define.
    /// </summary>
    public const int MaxPerOrganization = 50;

    /// <summary>
    /// Organization identifier.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Display name, unique in the organization.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Optional description of what the role is for.
    /// </summary>
    [MaxLength(DescriptionMaxLength)]
    public string Description { get; set; }

    /// <summary>
    /// Permissions of the role (normalized by <see cref="OrgRoleRules.NormalizeCustom"/>).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<OrgPermission> Permissions { get; set; } = [];

    /// <summary>
    /// The seeded role whose notifications this role receives (<see cref="OrgRoleRules.AudienceRoleOf"/>), kept with the
    /// permissions so the notification recipients are found in one query.
    /// </summary>
    public OrgRole AudienceRole { get; set; } = OrgRole.Viewer;

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
