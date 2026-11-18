using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.RevokeInvitation;

/// <summary>
/// Revokes a pending invitation.
/// </summary>
[OrgAuth(OrgPermission.ManageMembers)]
public record RevokeInvitationCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Invitation id.
    /// </summary>
    public int InvitationId { get; set; }
}
