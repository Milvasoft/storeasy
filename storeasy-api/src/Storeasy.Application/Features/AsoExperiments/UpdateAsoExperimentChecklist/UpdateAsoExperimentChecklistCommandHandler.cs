using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Application.Utils.Experiments;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Content;
using Storeasy.Domain.Experiments;
using System.Globalization;

namespace Storeasy.Application.Features.AsoExperiments.UpdateAsoExperimentChecklist;

/// <summary>
/// Handles <see cref="UpdateAsoExperimentChecklistCommand"/>. Only Play records have a checklist.
/// </summary>
/// <param name="ExperimentRepository"></param>
/// <param name="PlanRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
[Log]
public record UpdateAsoExperimentChecklistCommandHandler(IStoreasyRepositoryBase<AsoExperiment> ExperimentRepository,
                                                         IStoreasyRepositoryBase<ChangePlan> PlanRepository,
                                                         IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                                         IAuditTrail AuditTrail,
                                                         IMilvaLocalizer Localizer,
                                                         TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UpdateAsoExperimentChecklistCommand, AsoExperimentDto>
{
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = ExperimentRepository;
    private readonly IStoreasyRepositoryBase<ChangePlan> _planRepository = PlanRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AsoExperimentDto>> Handle(UpdateAsoExperimentChecklistCommand request, CancellationToken cancellationToken)
    {
        var record = await _experimentRepository.FindAsync(request.OrganizationId, request.Id, cancellationToken);

        if (record == null)
            return Response<AsoExperimentDto>.Error(null, MessageKey.AsoExperimentNotFound);

        var step = AsoExperimentViews.StepOf(record.Kind);
        var done = (request.DoneItems ?? []).Distinct().Order().ToList();

        if (step == null || done.Any(n => n < 1 || n > step.StepCount))
            return Response<AsoExperimentDto>.Error(null, MessageKey.AsoChecklistInvalid);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _experimentRepository.ExecuteUpdateAsync(record.Id,
                                                       new SetPropertyBuilder<AsoExperiment>().SetPropertyValue(r => r.ChecklistDone, done),
                                                       cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AsoExperimentUpdated,
                                     record.Id.ToString(CultureInfo.InvariantCulture),
                                     $"{record.Kind} '{record.Name}': {done.Count} of {step.StepCount} Play Console steps done.",
                                     record.Store,
                                     new { record.Id, DoneItems = done },
                                     cancellationToken);

        var dto = await _experimentRepository.DetailAsync(_planRepository, _storeAppRepository, request.OrganizationId, record.Id, now, _localizer, cancellationToken);

        return Response<AsoExperimentDto>.Success(dto);
    }
}
