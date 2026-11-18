using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetPricePoints;

/// <summary>
/// Handles <see cref="GetPricePointsQuery"/>.
/// </summary>
/// <param name="contentService"></param>
public class GetPricePointsQueryHandler(IStoreContentService contentService) : IInterceptable, IQueryHandler<GetPricePointsQuery, List<StoreOptionDto>>
{
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<List<StoreOptionDto>>> Handle(GetPricePointsQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null)
            return Response<List<StoreOptionDto>>.Error(null, MessageKey.StoreAppNotFound);

        if (app.Store != StoreKind.Apple)
            return Response<List<StoreOptionDto>>.Error(null, MessageKey.StoreOperationUnsupported);

        var options = await _contentService.ListOptionsAsync(app, StoreOptionKind.PricePoints, request.Territory, cancellationToken);

        if (options.ErrorKey != null)
            return Response<List<StoreOptionDto>>.Error(null, new ResponseMessage(options.ErrorKey, options.ErrorDetail ?? options.ErrorKey));

        return Response<List<StoreOptionDto>>.Success([.. options.Options.Select(o => new StoreOptionDto { Key = o.Key, Title = o.Title, Extra = o.Extra })]);
    }
}
