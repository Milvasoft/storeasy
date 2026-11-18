using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.AppDtos;

namespace Storeasy.Application.Features.Apps.GetAppList;

/// <summary>
/// Pages through the organization's apps with their store records (all stores in one list). <see cref="Store"/> narrows
/// it to the apps that have a record in that store, on the server, so paging and the total count follow the filter.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAppListQuery : ListRequest, IListRequestQuery<AppListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only apps with a record in this store; null for every app.
    /// </summary>
    public StoreKind? Store { get; set; }
}
