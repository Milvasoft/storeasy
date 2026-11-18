namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// Members and pending invitations.
/// </summary>
public class MemberListDto
{
    /// <summary>
    /// Members ordered by role, then e-mail.
    /// </summary>
    public List<OrganizationMemberDto> Members { get; set; }

    /// <summary>
    /// Pending, unexpired invitations. Empty for callers without <see cref="OrgPermission.ManageMembers"/>.
    /// </summary>
    public List<OrganizationInvitationDto> Invitations { get; set; }
}
