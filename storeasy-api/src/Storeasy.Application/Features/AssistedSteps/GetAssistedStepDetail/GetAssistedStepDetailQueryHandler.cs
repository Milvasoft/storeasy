using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.GetAssistedStepDetail;

/// <summary>
/// Handles <see cref="GetAssistedStepDetailQuery"/>.
/// </summary>
/// <param name="stepRepository"></param>
/// <param name="answerRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="rulesCatalog"></param>
/// <param name="localizer"></param>
public class GetAssistedStepDetailQueryHandler(IStoreasyRepositoryBase<AssistedStep> stepRepository,
                                               IStoreasyRepositoryBase<SavedAnswer> answerRepository,
                                               IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                               IRulesCatalog rulesCatalog,
                                               IMilvaLocalizer localizer) : IInterceptable, IQueryHandler<GetAssistedStepDetailQuery, AssistedStepDetailDto>
{
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = stepRepository;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = answerRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<Response<AssistedStepDetailDto>> Handle(GetAssistedStepDetailQuery request, CancellationToken cancellationToken)
    {
        var step = await _stepRepository.GetFirstOrDefaultAsync(s => s.Id == request.StepId && s.OrganizationId == request.OrganizationId,
                                                                projection: AssistedStepProjections.Detail,
                                                                cancellationToken: cancellationToken);

        if (step == null)
            return Response<AssistedStepDetailDto>.Error(null, MessageKey.AssistedStepNotFound);

        var catalog = _rulesCatalog.Current;
        var definition = AssistedStepCatalog.Find(catalog, step.Store, step.Key);

        if (definition == null)
            return Response<AssistedStepDetailDto>.Error(null, MessageKey.AssistedStepNotFound);

        var externalId = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == step.AppId && s.Store == step.Store && s.OrganizationId == request.OrganizationId,
                                                                          projection: s => s.ExternalId,
                                                                          cancellationToken: cancellationToken);

        var answers = await _answerRepository.GetAllAsync(a => a.AppId == step.AppId && a.OrganizationId == request.OrganizationId && definition.AnswerKeys.Contains(a.Kind),
                                                          projection: a => new SavedAnswer { Kind = a.Kind, Answers = a.Answers },
                                                          cancellationToken: cancellationToken);

        var detail = ContentViews.FillStep(new AssistedStepDetailDto(), step, definition, catalog, _localizer, System.Globalization.CultureInfo.CurrentUICulture.Name);

        detail.StoreUrl = definition.StoreUrl(externalId);
        detail.Steps = ContentViews.Checklist(definition, step.DoneItems, _localizer);
        detail.Pitfalls = ContentViews.Pitfalls(definition, _localizer);
        detail.AnswerKeys = [.. definition.AnswerKeys];
        detail.Answers = ContentViews.Answers(answers, definition.AnswerKeys);
        detail.HandedOffDate = step.HandedOffDate;
        detail.HandedOffByUserName = step.HandedOffByUserName;
        detail.Note = step.Note;
        detail.EvidenceAssetId = step.EvidenceAssetId;

        return Response<AssistedStepDetailDto>.Success(detail);
    }
}
