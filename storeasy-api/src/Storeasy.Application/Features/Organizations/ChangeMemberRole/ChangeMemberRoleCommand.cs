using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.ChangeMemberRole;

/// <summary>
/// Changes a member's role, to a seeded role or to one of the organization's custom roles. Only owners touch or create
/// owners; anyone else may neither change a member who can do more than they can nor give more than they hold; the last
/// owner cannot be demoted.
/// </summary>
[OrgAuth(OrgPermission.ManageMembers)]
public record ChangeMemberRoleCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Membership id.
    /// </summary>
    public int MemberId { get; set; }

    /// <summary>
    /// New role.
    /// </summary>
    public OrgRole Role { get; set; }

    /// <summary>
    /// The custom role when <see cref="Role"/> is <see cref="OrgRole.Custom"/>.
    /// </summary>
    public int? CustomRoleId { get; set; }
}
