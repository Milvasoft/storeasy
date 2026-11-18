using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Monetization;
using Storeasy.Infrastructure.Services;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Monetization;

/// <summary>
/// Change plans of subscriptions, In-App Purchases and purchase settings (<see cref="ChangePlanKind.Monetization"/>,
/// <see cref="ChangePlanKind.ServerNotifications"/>, <see cref="ChangePlanKind.SandboxTesters"/>): on approval the touched
/// products are read again for the fingerprint; the apply job writes through the store's
/// <see cref="IMonetizationGateway"/> and queues a sync, so the money screens show the store's new state.
/// </summary>
/// <param name="gateways"></param>
/// <param name="storeAccess"></param>
/// <param name="storeJobQueue"></param>
public sealed class MonetizationPlanExtension(IEnumerable<IMonetizationGateway> gateways,
                                             MonetizationStoreAccess storeAccess,
                                             IStoreJobQueue storeJobQueue) : IChangePlanExtension
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IEnumerable<IMonetizationGateway> _gateways = gateways;
    private readonly MonetizationStoreAccess _storeAccess = storeAccess;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;

    /// <inheritdoc/>
    public bool Handles(ChangePlanKind kind) => kind is ChangePlanKind.Monetization or ChangePlanKind.ServerNotifications or ChangePlanKind.SandboxTesters;

    /// <inheritdoc/>
    public async Task<StoreContentRead> ReadBaseAsync(StoreAppContext app, IReadOnlyCollection<string> baseKeys, CancellationToken cancellationToken)
    {
        var (parts, productIds) = MonetizationContent.ScopeOf(baseKeys);
        var read = await _storeAccess.ReadAsync(app, parts, productIds, cancellationToken);

        return read.IsSuccess ? new StoreContentRead(MonetizationContent.AsContent(read.Snapshot)) : new StoreContentRead(null, read.ErrorKey, read.ErrorDetail);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(ChangePlanApplyRequest request, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == request.Store) ?? throw new StoreJobException($"{request.Store} has no monetization support.", transient: false);
        var changes = JsonSerializer.Deserialize<MonetizationChangeSet>(request.Changes, _json) ?? new MonetizationChangeSet();
        var outcome = await gateway.ApplyAsync(request.Target, changes, context, cancellationToken);

        if (request.Kind == ChangePlanKind.Monetization)
            await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.SyncMonetization, new SyncMonetizationPayload(request.StoreAppId), request.Store, request.AppId)], cancellationToken);

        return outcome;
    }

    /// <inheritdoc/>
    public AuditAction AuditActionOf(ChangePlanKind kind, PlanItem item)
        => kind == ChangePlanKind.Monetization ? MonetizationRules.AuditActionOf(item.Group) : AuditAction.PurchaseSettingsChanged;
}
