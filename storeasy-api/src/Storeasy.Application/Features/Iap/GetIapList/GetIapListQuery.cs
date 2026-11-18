using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapList;

/// <summary>
/// Lists the In-App Purchases (App Store) or one-time products (Play) of a store record as last synced.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetIapListQuery : ListRequest, IListRequestQuery<IapListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Search in product ids and reference names.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Only this kind.
    /// </summary>
    public StoreProductKind? Kind { get; set; }

    /// <summary>
    /// Only this state.
    /// </summary>
    public ProductState? State { get; set; }
}
