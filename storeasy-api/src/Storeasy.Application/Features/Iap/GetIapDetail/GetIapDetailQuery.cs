using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapDetail;

/// <summary>
/// Reads an In-App Purchase (App Store) or one-time product (Play) as last synced.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetIapDetailQuery : IQuery<ProductDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy id of the product.
    /// </summary>
    public long IapId { get; set; }
}
