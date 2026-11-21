using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Experiments;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Features.AsoExperiments.PlanAsoExperiment;

/// <summary>
/// Handles <see cref="PlanAsoExperimentCommand"/>: reads the store record's experiments live and saves what they say
/// (so the plan starts from the store's state), inspects the files the variants use, builds the draft
/// (<see cref="AsoExperimentPlanner"/>) and saves it through <see cref="IChangePlanService"/>. Applying a winner reads the
/// editable app version's media as well. The plan is listed on the record's timeline.
/// </summary>
/// <param name="ExperimentRepository"></param>
/// <param name="ContentService"></param>
/// <param name="ExperimentStore"></param>
/// <param name="ChangePlanService"></param>
/// <param name="RulesCatalog"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
[Log]
public record PlanAsoExperimentCommandHandler(IStoreasyRepositoryBase<AsoExperiment> ExperimentRepository,
                                              IStoreContentService ContentService,
                                              IAsoExperimentStore ExperimentStore,
                                              IChangePlanService ChangePlanService,
                                              IRulesCatalog RulesCatalog,
                                              IMilvaLocalizer Localizer,
                                              TimeProvider TimeProvider) : IInterceptable, ICommandHandler<PlanAsoExperimentCommand, ChangePlanDto>
{
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = ExperimentRepository;
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IAsoExperimentStore _experimentStore = ExperimentStore;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IRulesCatalog _rulesCatalog = RulesCatalog;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanAsoExperimentCommand request, CancellationToken cancellationToken)
    {
        var record = await _experimentRepository.FindAsync(request.OrganizationId, request.Id, cancellationToken);

        if (record == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.AsoExperimentNotFound);

        if (!AsoExperimentRules.IsApple(record.Kind))
            return Response<ChangePlanDto>.Error(null, MessageKey.AsoExperimentNoApi);

        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, record.StoreAppId, cancellationToken);

        if (app == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

        var read = await _experimentStore.ReadAsync(app, cancellationToken);

        if (!read.IsSuccess)
            return Response<ChangePlanDto>.Error(null, Message(read.ErrorKey, read.ErrorDetail));

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (AsoExperimentContent.Merge(record, read.Read))
            await AsoExperimentStoreSync.SaveStateAsync(_experimentRepository, record, now, cancellationToken);

        var winner = request.Operation == AsoExperimentOperation.ApplyWinner ? request.WinnerVariantKey ?? record.WinnerVariantKey : null;
        var variants = winner == null ? record.Variants : record.Variants.Where(v => v.Key == winner).ToList();
        var files = await _contentService.InspectAssetsAsync(request.OrganizationId, AsoExperimentPlanner.AssetIdsOf(variants), cancellationToken);

        StoreContentSnapshot version = null;

        if (request.Operation == AsoExperimentOperation.ApplyWinner)
        {
            var versionRead = await _contentService.ReadAsync(app, ContentParts.Screenshots | ContentParts.Previews, null, cancellationToken);

            if (!versionRead.IsSuccess)
                return Response<ChangePlanDto>.Error(null, Message(versionRead.ErrorKey, versionRead.ErrorDetail));

            version = versionRead.Snapshot;
        }

        var result = AsoExperimentPlanner.Build(new AsoPlanInput(app, record, request.Operation, request.WithVersion, winner, read.Read, files, version, _rulesCatalog.Current, _localizer));

        if (!result.IsSuccess)
            return Response<ChangePlanDto>.Error(null, Message(result.ErrorKey, result.ErrorDetail));

        var snapshot = request.Operation == AsoExperimentOperation.ApplyWinner ? version : AsoExperimentContent.AsContent(read.Read);
        var saved = await _changePlanService.SaveAsync(app, snapshot, result.Draft, request.DryRun, request.Confirm, cancellationToken);

        if (saved.Data?.PlanId is long planId && planId > 0)
        {
            var builder = new SetPropertyBuilder<AsoExperiment>().SetPropertyValue(r => r.PlanIds, [.. record.PlanIds ?? [], planId]);

            if (winner != null)
                builder = builder.SetPropertyValue(r => r.WinnerVariantKey, winner);

            await _experimentRepository.ExecuteUpdateAsync(record.Id, builder, cancellationToken);
        }

        return saved;
    }

    private ResponseMessage Message(string key, string detail)
    {
        string text = _localizer[key, detail ?? string.Empty];

        if (detail != null && !text.Contains(detail, StringComparison.Ordinal))
            text = $"{text} ({detail})";

        return new ResponseMessage(key, text);
    }
}
