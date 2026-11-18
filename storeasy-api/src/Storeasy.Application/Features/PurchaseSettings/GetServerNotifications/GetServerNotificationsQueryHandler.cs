using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Utils.Monetization;

namespace Storeasy.Application.Features.PurchaseSettings.GetServerNotifications;

/// <summary>
/// Handles <see cref="GetServerNotificationsQuery"/>.
/// </summary>
/// <param name="monetizationService"></param>
/// <param name="contentService"></param>
/// <param name="readinessService"></param>
public class GetServerNotificationsQueryHandler(IMonetizationService monetizationService,
                                                IStoreContentService contentService,
                                                IReadinessService readinessService) : IInterceptable, IQueryHandler<GetServerNotificationsQuery, ServerNotificationsDto>
{
    private readonly IMonetizationService _monetizationService = monetizationService;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IReadinessService _readinessService = readinessService;

    /// <inheritdoc/>
    public async Task<Response<ServerNotificationsDto>> Handle(GetServerNotificationsQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null)
            return Response<ServerNotificationsDto>.Error(null, MessageKey.StoreAppNotFound);

        var result = new ServerNotificationsDto { StoreAppId = app.StoreAppId, Store = app.Store };

        if (app.Store != StoreKind.Apple)
        {
            var steps = await _readinessService.GetStepsAsync(request.OrganizationId, app.AppId, cancellationToken);
            var step = steps.GetValueOrDefault((app.AppId, app.Store))?.GetValueOrDefault(MonetizationAssistedSteps.RealTimeNotifications);

            result.Assisted = true;
            result.AssistedStepKey = MonetizationAssistedSteps.RealTimeNotifications;
            result.AssistedStepId = step?.Id;
            result.AssistedStepStatus = step?.Status;

            return Response<ServerNotificationsDto>.Success(result);
        }

        var read = await _monetizationService.ReadAsync(app, MonetizationParts.Products, [], cancellationToken);

        if (!read.IsSuccess)
            return read.Error<ServerNotificationsDto>();

        result.Url = read.Snapshot.NotificationUrl;
        result.Version = read.Snapshot.NotificationVersion;
        result.SandboxUrl = read.Snapshot.SandboxNotificationUrl;
        result.SandboxVersion = read.Snapshot.SandboxNotificationVersion;

        return Response<ServerNotificationsDto>.Success(result);
    }
}
