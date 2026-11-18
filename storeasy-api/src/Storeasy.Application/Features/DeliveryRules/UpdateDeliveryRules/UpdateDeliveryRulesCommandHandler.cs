using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.DeliveryRules.UpdateDeliveryRules;

/// <summary>
/// Handles <see cref="UpdateDeliveryRulesCommand"/>.
/// </summary>
/// <param name="RuleRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="TransactionRunner"></param>
/// <param name="TimeProvider"></param>
[Log]
public record UpdateDeliveryRulesCommandHandler(IStoreasyRepositoryBase<DeliveryRule> RuleRepository,
                                               IStoreasyRepositoryBase<App> AppRepository,
                                               ICurrentUser CurrentUser,
                                               IAuditTrail AuditTrail,
                                               ITransactionRunner TransactionRunner,
                                               TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UpdateDeliveryRulesCommand, int>
{
    private readonly IStoreasyRepositoryBase<DeliveryRule> _ruleRepository = RuleRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateDeliveryRulesCommand request, CancellationToken cancellationToken)
    {
        if (request.Rules.Exists(r => !IntakeRules.IsTargetAllowed(r.Platform, r.Target, r.TargetId)))
            return Response<int>.Error(0, MessageKey.DeliveryTargetNotAllowed);

        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var rows = request.Rules.Select(r => new DeliveryRule
        {
            OrganizationId = request.OrganizationId,
            AppId = request.AppId,
            Platform = r.Platform,
            Enabled = r.Enabled,
            Target = r.Target,
            TargetId = r.TargetId?.Trim(),
            TargetName = string.IsNullOrWhiteSpace(r.TargetName) ? r.TargetId?.Trim() : r.TargetName.Trim(),
            OnWarning = r.OnWarning,
            RolloutFraction = r.Target == DeliveryTarget.PlayTrack ? r.RolloutFraction : null,
            UpdatedByUserId = _currentUser.Id,
            UpdatedByUserName = _currentUser.UserName,
            UpdatedDate = now,
        }).ToList();

        await _transactionRunner.RunAsync(async () =>
        {
            await _ruleRepository.ExecuteDeleteAsync(r => r.AppId == request.AppId && r.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

            if (rows.Count > 0)
                await _ruleRepository.AddRangeAsync(rows, cancellationToken);
        });

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.DeliveryRulesUpdated,
                                     request.AppId.ToString(),
                                     $"Delivery rules of app {request.AppId} updated ({rows.Count}).",
                                     details: rows.Select(r => new { r.Platform, r.Enabled, r.Target, r.TargetId, r.OnWarning, r.RolloutFraction }),
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(rows.Count);
    }
}
