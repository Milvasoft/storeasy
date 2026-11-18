using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Shared steps of the subscription, In-App Purchase and purchase settings handlers.
/// </summary>
public static class MonetizationExtensions
{
    /// <summary>
    /// A synced product of the organization (a product of another organization is not found), or null.
    /// </summary>
    /// <param name="productRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="productId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<StoreProduct> GetProductAsync(this IStoreasyRepositoryBase<StoreProduct> productRepository, int organizationId, long productId, CancellationToken cancellationToken)
        => productRepository.GetFirstOrDefaultAsync(p => p.Id == productId && p.OrganizationId == organizationId, projection: StoreProduct.Projections.Detail, cancellationToken: cancellationToken);

    /// <summary>
    /// Plans <paramref name="changes"/> on store record <paramref name="storeAppId"/>: the record must belong to the
    /// organization; changes naming a product by its storeasy id get its product id (a product of another record is not
    /// found).
    /// </summary>
    /// <param name="monetizationService"></param>
    /// <param name="contentService"></param>
    /// <param name="productRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="kind"></param>
    /// <param name="changes"></param>
    /// <param name="dryRun"></param>
    /// <param name="confirm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<Response<ChangePlanDto>> PlanChangesAsync(this IMonetizationService monetizationService,
                                                                       IStoreContentService contentService,
                                                                       IStoreasyRepositoryBase<StoreProduct> productRepository,
                                                                       int organizationId,
                                                                       int storeAppId,
                                                                       ChangePlanKind kind,
                                                                       List<MonetizationChangeDto> changes,
                                                                       bool dryRun,
                                                                       string confirm,
                                                                       CancellationToken cancellationToken)
    {
        var app = await contentService.GetStoreAppAsync(organizationId, storeAppId, cancellationToken);

        if (app == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

        var ids = changes.Where(c => c.SubscriptionId != null).Select(c => c.SubscriptionId.Value).Distinct().ToList();

        if (ids.Count > 0)
        {
            var products = await productRepository.GetAllAsync(p => ids.Contains(p.Id) && p.StoreAppId == storeAppId && p.OrganizationId == organizationId,
                                                               projection: p => new StoreProduct { Id = p.Id, ProductId = p.ProductId },
                                                               cancellationToken: cancellationToken);

            foreach (var change in changes.Where(c => c.SubscriptionId != null))
            {
                var product = products.FirstOrDefault(p => p.Id == change.SubscriptionId);

                if (product == null)
                    return Response<ChangePlanDto>.Error(null, MessageKey.MonetizationProductNotFound);

                change.ProductId = product.ProductId;
            }
        }

        return await monetizationService.PlanAsync(app, kind, changes, dryRun, confirm, cancellationToken);
    }

    /// <summary>
    /// The territory price table of a synced product (of one Play base plan or purchase option when
    /// <paramref name="planId"/> is set), searched by territory name and filtered, then paged.
    /// </summary>
    /// <param name="productRepository"></param>
    /// <param name="request"></param>
    /// <param name="organizationId"></param>
    /// <param name="productId"></param>
    /// <param name="planId"></param>
    /// <param name="territoryName"></param>
    /// <param name="source"></param>
    /// <param name="hasPendingChange"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ListResponse<ProductPriceDto>> GetPricesAsync(this IStoreasyRepositoryBase<StoreProduct> productRepository,
                                                                           ListRequest request,
                                                                           int organizationId,
                                                                           long productId,
                                                                           string planId,
                                                                           string territoryName,
                                                                           PriceSource? source,
                                                                           bool? hasPendingChange,
                                                                           CancellationToken cancellationToken)
    {
        var product = await productRepository.GetFirstOrDefaultAsync(p => p.Id == productId && p.OrganizationId == organizationId,
                                                                     projection: p => new StoreProduct { Id = p.Id, BaseTerritory = p.BaseTerritory, Prices = p.Prices, Plans = p.Plans },
                                                                     cancellationToken: cancellationToken);

        if (product == null)
            return ListResponse<ProductPriceDto>.Error(default, MessageKey.MonetizationProductNotFound);

        var plan = planId == null ? null : product.Plans?.FirstOrDefault(p => p.Id == planId);

        if (planId != null && plan == null)
            return ListResponse<ProductPriceDto>.Error(default, MessageKey.MonetizationBasePlanNotFound);

        var rows = MonetizationViews.Prices(plan == null ? product.BaseTerritory : null, plan?.Prices ?? product.Prices)
                                    .Where(p => string.IsNullOrWhiteSpace(territoryName) || (p.TerritoryName ?? string.Empty).Contains(territoryName.Trim(), StringComparison.OrdinalIgnoreCase))
                                    .Where(p => source == null || p.Source == source)
                                    .Where(p => hasPendingChange == null || p.HasPendingChange == hasPendingChange)
                                    .ToList();

        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(10);

        return rows.AsQueryable().ToListResponse(request);
    }

    /// <summary>
    /// The store's price points of a synced product in one territory, read live.
    /// </summary>
    /// <param name="monetizationService"></param>
    /// <param name="contentService"></param>
    /// <param name="productRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="productId"></param>
    /// <param name="territory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<Response<List<PricePointDto>>> GetPricePointsAsync(this IMonetizationService monetizationService,
                                                                                 IStoreContentService contentService,
                                                                                 IStoreasyRepositoryBase<StoreProduct> productRepository,
                                                                                 int organizationId,
                                                                                 long productId,
                                                                                 string territory,
                                                                                 CancellationToken cancellationToken)
    {
        var product = await productRepository.GetFirstOrDefaultAsync(p => p.Id == productId && p.OrganizationId == organizationId,
                                                                     projection: p => new StoreProduct { Id = p.Id, StoreAppId = p.StoreAppId, Kind = p.Kind, ExternalId = p.ExternalId },
                                                                     cancellationToken: cancellationToken);

        if (product == null)
            return Response<List<PricePointDto>>.Error(null, MessageKey.MonetizationProductNotFound);

        var app = await contentService.GetStoreAppAsync(organizationId, product.StoreAppId, cancellationToken);

        if (app == null)
            return Response<List<PricePointDto>>.Error(null, MessageKey.StoreAppNotFound);

        return await monetizationService.ListPricePointsAsync(app, product.Kind, product.ExternalId, territory.Trim().ToUpperInvariant(), cancellationToken);
    }

    /// <summary>
    /// The detail of a synced product of one of <paramref name="kinds"/>, with whether it must go to review with an app
    /// version.
    /// </summary>
    /// <param name="productRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="productId"></param>
    /// <param name="kinds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<Response<ProductDetailDto>> GetProductDetailAsync(this IStoreasyRepositoryBase<StoreProduct> productRepository,
                                                                                int organizationId,
                                                                                long productId,
                                                                                IReadOnlyCollection<StoreProductKind> kinds,
                                                                                CancellationToken cancellationToken)
    {
        var product = await productRepository.GetProductAsync(organizationId, productId, cancellationToken);

        if (product == null || !kinds.Contains(product.Kind))
            return Response<ProductDetailDto>.Error(null, MessageKey.MonetizationProductNotFound);

        var states = await productRepository.GetAllAsync(p => p.StoreAppId == product.StoreAppId && p.OrganizationId == organizationId,
                                                         projection: p => new StoreProduct { State = p.State },
                                                         cancellationToken: cancellationToken);

        return Response<ProductDetailDto>.Success(MonetizationViews.Detail(product, MonetizationViews.SubmitWithVersion(product, states.Select(s => s.State))));
    }
}
