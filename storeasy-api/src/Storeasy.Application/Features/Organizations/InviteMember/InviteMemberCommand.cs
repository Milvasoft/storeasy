using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.Organizations.InviteMember;

/// <summary>
/// Invites an e-mail address to the organization with a role (never owner; owners are promoted from members).
/// </summary>
[OrgAuth(OrgPermission.ManageMembers)]
public record InviteMemberCommand : ICommand<InvitationCreatedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// E-mail to invite.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Role on acceptance.
    /// </summary>
    public OrgRole Role { get; set; }
}
