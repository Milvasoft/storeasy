using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.AppDtos;

namespace Storeasy.Application.Features.Apps.GetAppList;

/// <summary>
/// Pages through the organization's apps with their store records (all stores in one list).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAppListQuery : ListRequest, IListRequestQuery<AppListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
