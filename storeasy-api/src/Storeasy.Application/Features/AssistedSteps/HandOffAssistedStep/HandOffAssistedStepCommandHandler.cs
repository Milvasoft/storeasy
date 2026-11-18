using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.HandOffAssistedStep;

/// <summary>
/// Handles <see cref="HandOffAssistedStepCommand"/>.
/// </summary>
/// <param name="StepRepository"></param>
/// <param name="AnswerRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="RulesCatalog"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
[Log]
public record HandOffAssistedStepCommandHandler(IStoreasyRepositoryBase<AssistedStep> StepRepository,
                                                IStoreasyRepositoryBase<SavedAnswer> AnswerRepository,
                                                IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                                ICurrentUser CurrentUser,
                                                IAuditTrail AuditTrail,
                                                IRulesCatalog RulesCatalog,
                                                IMilvaLocalizer Localizer,
                                                TimeProvider TimeProvider) : IInterceptable, ICommandHandler<HandOffAssistedStepCommand, AssistedStepHandOffDto>
{
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = StepRepository;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = AnswerRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly IRulesCatalog _rulesCatalog = RulesCatalog;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AssistedStepHandOffDto>> Handle(HandOffAssistedStepCommand request, CancellationToken cancellationToken)
    {
        var step = await _stepRepository.GetFirstOrDefaultAsync(s => s.Id == request.StepId && s.OrganizationId == request.OrganizationId,
                                                                projection: AssistedStepProjections.Detail,
                                                                cancellationToken: cancellationToken);

        var definition = step == null ? null : AssistedStepCatalog.Find(_rulesCatalog.Current, step.Store, step.Key);

        if (definition == null)
            return Response<AssistedStepHandOffDto>.Error(null, MessageKey.AssistedStepNotFound);

        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == step.AppId && s.Store == step.Store && s.OrganizationId == request.OrganizationId,
                                                                      projection: s => new StoreApp { ExternalId = s.ExternalId, Name = s.App.Name },
                                                                      cancellationToken: cancellationToken);

        var answers = await _answerRepository.GetAllAsync(a => a.AppId == step.AppId && a.OrganizationId == request.OrganizationId && definition.AnswerKeys.Contains(a.Kind),
                                                          projection: a => new SavedAnswer { Kind = a.Kind, Answers = a.Answers },
                                                          cancellationToken: cancellationToken);

        await _stepRepository.ExecuteUpdateAsync(step.Id,
                                                 new Milvasoft.DataAccess.EfCore.Utils.SetPropertyBuilder<AssistedStep>().SetPropertyValue(s => s.Status, step.Status is AssistedStepStatus.Done or AssistedStepStatus.Skipped ? step.Status : AssistedStepStatus.HandedOff)
                                                                                                                        .SetPropertyValue(s => s.HandedOffDate, (DateTime?)_timeProvider.GetUtcNow().UtcDateTime)
                                                                                                                        .SetPropertyValue(s => s.HandedOffByUserName, _currentUser.UserName),
                                                 cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.AssistedStepHandedOff, step.Id.ToString(), $"Assisted step {step.Key} of app {step.AppId} handed off.", step.Store, cancellationToken: cancellationToken);

        var title = ContentViews.Step(step, definition, _rulesCatalog.Current, _localizer, System.Globalization.CultureInfo.CurrentUICulture.Name).Title;
        var storeUrl = definition.StoreUrl(record?.ExternalId);
        const string tool = "assisted_step_complete";

        return Response<AssistedStepHandOffDto>.Success(new AssistedStepHandOffDto
        {
            StepId = step.Id,
            Key = step.Key,
            Store = step.Store,
            AppName = record?.Name,
            ExternalId = record?.ExternalId,
            Title = title,
            StoreUrl = storeUrl,
            Steps = ContentViews.Checklist(definition, step.DoneItems, _localizer),
            Pitfalls = ContentViews.Pitfalls(definition, _localizer),
            Answers = ContentViews.Answers(answers, definition.AnswerKeys),
            McpTool = tool,
            Prompt = _localizer[MessageKey.AssistedStepHandOffPrompt, storeUrl, title, record?.Name, tool],
        });
    }
}
