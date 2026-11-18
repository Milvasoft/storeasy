using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.ConvertSubscriptionPrices;

/// <summary>
/// Handles <see cref="ConvertSubscriptionPricesQuery"/>.
/// </summary>
/// <param name="monetizationService"></param>
/// <param name="contentService"></param>
public class ConvertSubscriptionPricesQueryHandler(IMonetizationService monetizationService,
                                                   IStoreContentService contentService) : IInterceptable, IQueryHandler<ConvertSubscriptionPricesQuery, ConvertedPricesDto>
{
    private readonly IMonetizationService _monetizationService = monetizationService;
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<ConvertedPricesDto>> Handle(ConvertSubscriptionPricesQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null)
            return Response<ConvertedPricesDto>.Error(null, MessageKey.StoreAppNotFound);

        return await _monetizationService.ConvertPricesAsync(app, request.Currency.Trim().ToUpperInvariant(), request.Amount.Trim(), cancellationToken);
    }
}
