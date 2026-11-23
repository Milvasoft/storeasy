using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// <see cref="StoreJobKind.RegistrySync"/>: syncs the tracked packages of the job's organization (or the one of the payload)
/// with <see cref="IRegistrySyncer"/>. A package that could not be read fails the step but not the job; the next sync tries
/// it again.
/// </summary>
/// <param name="syncer"></param>
public sealed class RegistrySyncJobHandler(IRegistrySyncer syncer) : IStoreJobHandler
{
    private readonly IRegistrySyncer _syncer = syncer;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.RegistrySync;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<RegistrySyncPayload>() ?? new RegistrySyncPayload();
        var step = await context.BeginStepAsync("registries.read", StepDirection.Inbound, payload.TrackedPackageId == null ? "All packages" : $"Package {payload.TrackedPackageId}", cancellationToken: cancellationToken);
        var summary = await _syncer.SyncAsync(context.OrganizationId, payload.TrackedPackageId, payload.DueOnly, cancellationToken);

        await context.EndStepAsync(step,
                                   summary.Failed.Count == 0 ? StoreJobStepStatus.Succeeded : StoreJobStepStatus.Failed,
                                   summary.Failed.Count == 0 ? $"{summary.Read} packages, {summary.Changes} changes." : $"{summary.Read} packages; not read: {string.Join(", ", summary.Failed)}.",
                                   cancellationToken);

        return new StoreJobOutcome($"Package registries: {summary.Read} packages read, {summary.Changes} changes.", summary);
    }
}
