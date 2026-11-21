using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreJobDtos;

namespace Storeasy.Application.Features.AsoExperiments.SyncAsoExperiments;

/// <summary>
/// Handles <see cref="SyncAsoExperimentsCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="StoreJobQueue"></param>
public record SyncAsoExperimentsCommandHandler(IStoreContentService ContentService, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<SyncAsoExperimentsCommand, long>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SyncAsoExperimentsCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (app.Store != StoreKind.Apple)
            return Response<long>.Error(0, MessageKey.AsoExperimentNoApi);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.SyncAsoExperiments, new SyncAsoExperimentsPayload(app.StoreAppId), app.Store, app.AppId)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
