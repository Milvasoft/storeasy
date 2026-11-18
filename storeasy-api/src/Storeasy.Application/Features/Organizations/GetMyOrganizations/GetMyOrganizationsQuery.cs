using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.Organizations.GetMyOrganizations;

/// <summary>
/// Lists the organizations of the current user (for the organization switcher).
/// </summary>
public record GetMyOrganizationsQuery : IQuery<List<MyOrganizationDto>>
{
}
