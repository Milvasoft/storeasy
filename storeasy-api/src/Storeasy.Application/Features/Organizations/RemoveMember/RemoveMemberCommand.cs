using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.RemoveMember;

/// <summary>
/// Removes a member, or lets the caller leave when <see cref="MemberId"/> is the caller's own membership. Checked with
/// <see cref="OrgPermission.View"/> so anyone may leave; removing someone else needs
/// <see cref="OrgPermission.ManageMembers"/> (checked in the handler).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record RemoveMemberCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Membership id.
    /// </summary>
    public int MemberId { get; set; }
}
