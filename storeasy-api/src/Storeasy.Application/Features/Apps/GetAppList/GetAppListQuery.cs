using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.AppDtos;

namespace Storeasy.Application.Features.Apps.GetAppList;

/// <summary>
/// Pages through the organization's apps with their store records (all stores in one list). <see cref="Store"/> narrows
/// it to the apps that have a record in that store, on the server, so paging and the total count follow the filter.
/// Hidden apps are left out unless <see cref="Hidden"/> or <see cref="IncludeHidden"/> asks for them. Without a sort the
/// apps come in the order members chose (apps/app/order), then the apps never placed by name.
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

    /// <summary>
    /// False (the default) lists the shown apps; true lists only the apps a member hid.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// True lists shown and hidden apps together and ignores <see cref="Hidden"/> (for name lookups).
    /// </summary>
    public bool IncludeHidden { get; set; }
}
