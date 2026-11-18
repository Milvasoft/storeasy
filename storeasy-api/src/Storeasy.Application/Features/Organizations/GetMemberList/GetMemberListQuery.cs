using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.Organizations.GetMemberList;

/// <summary>
/// Lists the members and the pending invitations of an organization.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMemberListQuery : IQuery<MemberListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
