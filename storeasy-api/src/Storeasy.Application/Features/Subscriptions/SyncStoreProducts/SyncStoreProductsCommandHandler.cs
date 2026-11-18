using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Subscriptions.SyncStoreProducts;

/// <summary>
/// Handles <see cref="SyncStoreProductsCommand"/>.
/// </summary>
/// <param name="MonetizationService"></param>
/// <param name="ContentService"></param>
[Log]
public record SyncStoreProductsCommandHandler(IMonetizationService MonetizationService,
                                              IStoreContentService ContentService) : IInterceptable, ICommandHandler<SyncStoreProductsCommand, long>
{
    private readonly IMonetizationService _monetizationService = MonetizationService;
    private readonly IStoreContentService _contentService = ContentService;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SyncStoreProductsCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (app.ConnectionId == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotConnected);

        return Response<long>.Success(await _monetizationService.QueueSyncAsync(app, cancellationToken));
    }
}
