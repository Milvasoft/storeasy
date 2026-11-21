using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Content;
using Storeasy.Domain.Experiments;
using System.Globalization;

namespace Storeasy.Application.Features.AsoExperiments.RecordAsoExperimentResult;

/// <summary>
/// Handles <see cref="RecordAsoExperimentResultCommand"/>. A Play record marked running without a start date starts now;
/// one marked stopped or completed without an end date ends now.
/// </summary>
/// <param name="ExperimentRepository"></param>
/// <param name="PlanRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
[Log]
public record RecordAsoExperimentResultCommandHandler(IStoreasyRepositoryBase<AsoExperiment> ExperimentRepository,
                                                      IStoreasyRepositoryBase<ChangePlan> PlanRepository,
                                                      IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                                      IAuditTrail AuditTrail,
                                                      IMilvaLocalizer Localizer,
                                                      TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RecordAsoExperimentResultCommand, AsoExperimentDto>
{
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = ExperimentRepository;
    private readonly IStoreasyRepositoryBase<ChangePlan> _planRepository = PlanRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AsoExperimentDto>> Handle(RecordAsoExperimentResultCommand request, CancellationToken cancellationToken)
    {
        var record = await _experimentRepository.FindAsync(request.OrganizationId, request.Id, cancellationToken);

        if (record == null)
            return Response<AsoExperimentDto>.Error(null, MessageKey.AsoExperimentNotFound);

        var apple = AsoExperimentRules.IsApple(record.Kind);

        if (request.Status != null && (apple || request.Status is not (AsoExperimentStatus.Running or AsoExperimentStatus.Stopped or AsoExperimentStatus.Completed)))
            return Response<AsoExperimentDto>.Error(null, MessageKey.AsoResultStatusInvalid);

        if (apple && (request.StartDate != null || request.EndDate != null))
            return Response<AsoExperimentDto>.Error(null, MessageKey.AsoResultStatusInvalid);

        var winner = string.IsNullOrWhiteSpace(request.WinnerVariantKey) ? null : request.WinnerVariantKey.Trim();

        if (winner != null && !(record.Variants ?? []).Any(v => v.Key == winner))
            return Response<AsoExperimentDto>.Error(null, new ResponseMessage(MessageKey.AsoWinnerUnknown, _localizer[MessageKey.AsoWinnerUnknown, winner]));

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var status = request.Status ?? record.Status;
        var start = request.StartDate?.ToUniversalTime() ?? record.StartDate;
        var end = request.EndDate?.ToUniversalTime() ?? record.EndDate;

        if (!apple)
        {
            if (status == AsoExperimentStatus.Running)
                start ??= now;

            if (status is AsoExperimentStatus.Stopped or AsoExperimentStatus.Completed)
                end ??= now;
        }

        if (start != null && end != null && end < start)
            return Response<AsoExperimentDto>.Error(null, MessageKey.AsoResultStatusInvalid);

        var summary = string.IsNullOrWhiteSpace(request.ResultSummary) ? record.ResultSummary : request.ResultSummary.Trim();
        var url = string.IsNullOrWhiteSpace(request.ResultUrl) ? record.ResultUrl : request.ResultUrl.Trim();

        await _experimentRepository.ExecuteUpdateAsync(record.Id,
                                                       new SetPropertyBuilder<AsoExperiment>().SetPropertyValue(r => r.Status, status)
                                                                                               .SetPropertyValue(r => r.StartDate, start)
                                                                                               .SetPropertyValue(r => r.EndDate, end)
                                                                                               .SetPropertyValue(r => r.WinnerVariantKey, winner ?? record.WinnerVariantKey)
                                                                                               .SetPropertyValue(r => r.ResultSummary, summary)
                                                                                               .SetPropertyValue(r => r.ResultUrl, url),
                                                       cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AsoExperimentResultRecorded,
                                     record.Id.ToString(CultureInfo.InvariantCulture),
                                     $"{record.Kind} '{record.Name}': {status}{(winner == null ? string.Empty : $", winner {winner}")}.",
                                     record.Store,
                                     new { record.Id, Status = status, StartDate = start, EndDate = end, Winner = winner ?? record.WinnerVariantKey },
                                     cancellationToken);

        var dto = await _experimentRepository.DetailAsync(_planRepository, _storeAppRepository, request.OrganizationId, record.Id, now, _localizer, cancellationToken);

        return Response<AsoExperimentDto>.Success(dto);
    }
}
