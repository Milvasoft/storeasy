using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Reviews.SyncReviews;

/// <summary>
/// Handles <see cref="SyncReviewsCommand"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
public record SyncReviewsCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<SyncReviewsCommand, List<long>>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<List<long>>> Handle(SyncReviewsCommand request, CancellationToken cancellationToken)
    {
        var records = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == request.OrganizationId
                                                                  && (request.AppId == null || s.AppId == request.AppId)
                                                                  && (s.Store == StoreKind.Apple || s.Store == StoreKind.Google)
                                                                  && s.StoreConnectionId != null
                                                                  && !s.App.IsDeleted,
                                                             projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, Store = s.Store },
                                                             cancellationToken: cancellationToken);

        if (records.Count == 0)
            return Response<List<long>>.Error(null, MessageKey.StoreAppNotFound);

        var jobIds = await _storeJobQueue.EnqueueAsync([.. records.Select(r => new StoreJobRequest(request.OrganizationId, StoreJobKind.SyncReviews, new SyncReviewsPayload(r.Id), r.Store, r.AppId))], cancellationToken);

        return Response<List<long>>.Success(jobIds);
    }
}
