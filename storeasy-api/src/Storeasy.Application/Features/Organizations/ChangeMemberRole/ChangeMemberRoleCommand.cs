using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.ChangeMemberRole;

/// <summary>
/// Changes a member's role. Admins cannot touch owners or create owners; the last owner cannot be demoted.
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
}
