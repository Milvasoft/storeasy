using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.Organizations.InviteMember;

/// <summary>
/// Invites an e-mail address to the organization with a seeded role or one of its custom roles (never owner; owners are
/// promoted from members). Nobody but an owner gives more than they hold.
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

    /// <summary>
    /// The custom role when <see cref="Role"/> is <see cref="OrgRole.Custom"/>.
    /// </summary>
    public int? CustomRoleId { get; set; }
}
