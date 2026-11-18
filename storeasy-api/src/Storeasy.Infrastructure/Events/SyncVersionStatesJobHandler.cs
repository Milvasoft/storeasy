using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// The <see cref="StoreJobKind.SyncVersionStates"/> job: reads the newest App Store versions of every App Store record of
/// one connection and applies their states, so a rejection is caught even when no webhook arrived.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="gateway"></param>
/// <param name="tracker"></param>
public sealed class SyncVersionStatesJobHandler(IStoreasyRepositoryBase<StoreApp> storeAppRepository, IStoreEventGateway gateway, IVersionStateTracker tracker) : IStoreJobHandler
{
    private const int _versionsPerApp = 5;

    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreEventGateway _gateway = gateway;
    private readonly IVersionStateTracker _tracker = tracker;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.SyncVersionStates;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<SyncVersionStatesPayload>() ?? throw new StoreJobException("The job has no connection.", transient: false);
        var connection = await context.OpenConnectionAsync(payload.ConnectionId, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);

        if (connection.Store != StoreKind.Apple)
            throw new StoreJobException("Version states are read from App Store Connect only.", transient: false);

        var records = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == context.OrganizationId && s.Store == StoreKind.Apple && s.StoreConnectionId == connection.Id && !s.App.IsDeleted,
                                                            projection: s => new StoreApp { Id = s.Id, ExternalId = s.ExternalId },
                                                            cancellationToken: cancellationToken);

        var step = await context.BeginStepAsync("versions.list", StepDirection.Outbound, total: records.Count, cancellationToken: cancellationToken);
        var changes = new List<VersionStateChange>();

        for (var i = 0; i < records.Count; i++)
        {
            var versions = await _gateway.ListVersionsAsync(connection.SecretJson, records[i].ExternalId, cancellationToken);
            var record = records[i];

            changes.AddRange(versions.Take(_versionsPerApp).Select(v => new VersionStateChange(record.Id, v.Id, v.VersionString, v.BuildNumber, v.State, null)));

            await context.ReportProgressAsync(step, i + 1, cancellationToken);
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{changes.Count} versions.", cancellationToken);

        var applyStep = await context.BeginStepAsync("states.apply", StepDirection.Internal, cancellationToken: cancellationToken);
        var outcomes = await _tracker.ApplyAsync(context.OrganizationId, changes, RejectionSource.Sync, cancellationToken);
        var opened = outcomes.Count(o => o.OpenedCaseId.HasValue);

        await context.EndStepAsync(applyStep, StoreJobStepStatus.Succeeded, $"{outcomes.Count(o => o.Changed)} changed, {opened} rejection cases opened.", cancellationToken);

        return new StoreJobOutcome($"{changes.Count} versions read; {outcomes.Count(o => o.Changed)} changed, {opened} rejection cases opened.", new { Versions = changes.Count, Changed = outcomes.Count(o => o.Changed), Opened = opened });
    }
}
