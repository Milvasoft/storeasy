using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionGroups;

/// <summary>
/// Handles <see cref="GetSubscriptionGroupsQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="groupRepository"></param>
/// <param name="productRepository"></param>
public class GetSubscriptionGroupsQueryHandler(IStoreContentService contentService,
                                               IStoreasyRepositoryBase<ProductGroup> groupRepository,
                                               IStoreasyRepositoryBase<StoreProduct> productRepository) : IInterceptable, IQueryHandler<GetSubscriptionGroupsQuery, SubscriptionGroupsDto>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IStoreasyRepositoryBase<ProductGroup> _groupRepository = groupRepository;
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;

    /// <inheritdoc/>
    public async Task<Response<SubscriptionGroupsDto>> Handle(GetSubscriptionGroupsQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null)
            return Response<SubscriptionGroupsDto>.Error(null, MessageKey.StoreAppNotFound);

        var groups = await _groupRepository.GetAllAsync(g => g.StoreAppId == app.StoreAppId && g.OrganizationId == request.OrganizationId,
                                                        projection: g => new ProductGroup { Id = g.Id, ExternalId = g.ExternalId, ReferenceName = g.ReferenceName, Localizations = g.Localizations, SyncedDate = g.SyncedDate },
                                                        cancellationToken: cancellationToken);

        var products = await _productRepository.GetAllAsync(p => p.StoreAppId == app.StoreAppId && p.OrganizationId == request.OrganizationId && p.Kind == StoreProductKind.Subscription,
                                                            projection: StoreProduct.Projections.Detail,
                                                            cancellationToken: cancellationToken);

        var result = new SubscriptionGroupsDto
        {
            StoreAppId = app.StoreAppId,
            Store = app.Store,
            SyncedDate = products.Select(p => (DateTime?)p.SyncedDate).Concat(groups.Select(g => (DateTime?)g.SyncedDate)).Max(),
            Groups = [.. groups.OrderBy(g => g.ReferenceName, StringComparer.OrdinalIgnoreCase).Select(g => new SubscriptionGroupDto
            {
                Id = g.Id,
                ExternalId = g.ExternalId,
                ReferenceName = g.ReferenceName,
                Localizations = g.Localizations,
                Subscriptions = [.. products.Where(p => p.GroupId == g.Id).OrderBy(p => p.Level ?? int.MaxValue).Select(MonetizationViews.Summary)],
            })],
            PlaySubscriptions = [.. products.Where(p => p.Store == StoreKind.Google).OrderBy(p => p.ProductId, StringComparer.Ordinal).Select(MonetizationViews.PlaySubscription)],
        };

        return Response<SubscriptionGroupsDto>.Success(result);
    }
}
