using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.PlanDataSafety;

/// <summary>
/// Handles <see cref="PlanDataSafetyCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="AnswerRepository"></param>
[Log]
public record PlanDataSafetyCommandHandler(IStoreContentService ContentService,
                                           IChangePlanService ChangePlanService,
                                           IStoreasyRepositoryBase<SavedAnswer> AnswerRepository) : IInterceptable, ICommandHandler<PlanDataSafetyCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = AnswerRepository;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanDataSafetyCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, StoreKind.Google, cancellationToken);

        if (app == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

        var row = await _answerRepository.GetFirstOrDefaultAsync(a => a.AppId == request.AppId && a.OrganizationId == request.OrganizationId && a.Kind == SavedAnswerKind.DataSafety,
                                                                projection: a => new SavedAnswer { Answers = a.Answers },
                                                                cancellationToken: cancellationToken);

        var answers = SavedAnswerDocuments.Read<DataSafetyAnswers>(row?.Answers);

        if (answers == null || answers.Rows.Count == 0)
            return Response<ChangePlanDto>.Error(null, MessageKey.SavedAnswerNotFound);

        var draft = new PlanDraft
        {
            Kind = ChangePlanKind.DataSafety,
            Title = $"Data safety form ({answers.Rows.Count} answers)",
            Items = [new Storeasy.Domain.JsonModels.PlanItem { Group = "DataSafety", Field = "safetyLabels", After = $"{answers.Rows.Count} answers", Action = ChangeAction.Update }],
            Calls = [new Storeasy.Domain.JsonModels.PlanCall { Method = "POST", Path = "applications.dataSafety", Target = app.ExternalId }],
            Changes = new ContentChangeSet { DataSafetyCsv = DataSafetyCsv.Generate(answers) },
        };

        return await _changePlanService.SaveAsync(app, null, draft, request.DryRun, null, cancellationToken);
    }
}
