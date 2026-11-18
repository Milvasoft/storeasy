using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.CompleteAssistedStep;

/// <summary>
/// Handles <see cref="CompleteAssistedStepCommand"/>.
/// </summary>
/// <param name="StepRepository"></param>
/// <param name="AssetRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record CompleteAssistedStepCommandHandler(IStoreasyRepositoryBase<AssistedStep> StepRepository,
                                                 IStoreasyRepositoryBase<Storeasy.Domain.Assets.Asset> AssetRepository,
                                                 ICurrentUser CurrentUser,
                                                 IAuditTrail AuditTrail,
                                                 TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CompleteAssistedStepCommand, int>
{
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = StepRepository;
    private readonly IStoreasyRepositoryBase<Storeasy.Domain.Assets.Asset> _assetRepository = AssetRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CompleteAssistedStepCommand request, CancellationToken cancellationToken)
    {
        if (request.EvidenceAssetId != null)
        {
            var asset = await _assetRepository.GetFirstOrDefaultAsync(a => a.Id == request.EvidenceAssetId && a.OrganizationId == request.OrganizationId,
                                                                      projection: a => new Storeasy.Domain.Assets.Asset { Id = a.Id },
                                                                      cancellationToken: cancellationToken);

            if (asset == null)
                return Response<int>.Error(0, MessageKey.AssetNotFound);
        }

        var finished = request.Status is AssistedStepStatus.Done or AssistedStepStatus.Skipped;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var builder = new Milvasoft.DataAccess.EfCore.Utils.SetPropertyBuilder<AssistedStep>().SetPropertyValue(s => s.Status, request.Status)
                                                                                             .SetPropertyValue(s => s.Note, request.Note?.Trim())
                                                                                             .SetPropertyValue(s => s.EvidenceAssetId, request.EvidenceAssetId)
                                                                                             .SetPropertyValue(s => s.CompletedDate, finished ? now : null)
                                                                                             .SetPropertyValue(s => s.CompletedByUserName, finished ? _currentUser.UserName : null)
                                                                                             .SetPropertyValue(s => s.CompletedChannel, finished ? _auditTrail.Channel : null);

        if (request.DoneItems != null)
            builder = builder.SetPropertyValue(s => s.DoneItems, request.DoneItems.Distinct().Order().ToList());

        var step = await _stepRepository.GetFirstOrDefaultAsync(s => s.Id == request.StepId && s.OrganizationId == request.OrganizationId,
                                                                projection: s => new AssistedStep { Id = s.Id, Key = s.Key, AppId = s.AppId, Store = s.Store },
                                                                cancellationToken: cancellationToken);

        if (step == null)
            return Response<int>.Error(0, MessageKey.AssistedStepNotFound);

        await _stepRepository.ExecuteUpdateAsync(step.Id, builder, cancellationToken);

        if (finished)
        {
            await _auditTrail.WriteAsync(request.OrganizationId,
                                         AuditAction.AssistedStepCompleted,
                                         step.Id.ToString(),
                                         $"Assisted step {step.Key} of app {step.AppId} recorded as {request.Status}.",
                                         step.Store,
                                         new { step.Key, request.Status, request.EvidenceAssetId, request.Note },
                                         cancellationToken);
        }

        return Response<int>.Success(step.Id);
    }
}
