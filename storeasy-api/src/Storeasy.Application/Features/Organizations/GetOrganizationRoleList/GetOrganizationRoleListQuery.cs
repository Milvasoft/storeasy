using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.Organizations.GetOrganizationRoleList;

/// <summary>
/// Lists the roles of an organization: the four locked seeded roles with the matrix permissions, then the custom roles,
/// each with how many members and pending invitations hold it.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetOrganizationRoleListQuery : IQuery<List<OrgRoleDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
