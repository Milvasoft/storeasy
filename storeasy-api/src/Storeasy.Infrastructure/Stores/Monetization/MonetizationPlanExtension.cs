using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Monetization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Monetization;

/// <summary>
/// Change plans of subscriptions, In-App Purchases and purchase settings (<see cref="ChangePlanKind.Monetization"/>,
/// <see cref="ChangePlanKind.ServerNotifications"/>, <see cref="ChangePlanKind.SandboxTesters"/>): on approval the touched
/// products are read again for the fingerprint; the apply job writes through the store's
/// <see cref="IMonetizationGateway"/> and queues a sync, so the money screens show the store's new state.
/// </summary>
/// <param name="gateways"></param>
/// <param name="monetizationService"></param>
/// <param name="contentService"></param>
public sealed class MonetizationPlanExtension(IEnumerable<IMonetizationGateway> gateways,
                                             IMonetizationService monetizationService,
                                             IStoreContentService contentService) : IChangePlanExtension
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IEnumerable<IMonetizationGateway> _gateways = gateways;
    private readonly IMonetizationService _monetizationService = monetizationService;
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public bool Handles(ChangePlanKind kind) => kind is ChangePlanKind.Monetization or ChangePlanKind.ServerNotifications or ChangePlanKind.SandboxTesters;

    /// <inheritdoc/>
    public async Task<StoreContentRead> ReadBaseAsync(StoreAppContext app, IReadOnlyCollection<string> baseKeys, CancellationToken cancellationToken)
    {
        var (parts, productIds) = MonetizationContent.ScopeOf(baseKeys);
        var read = await _monetizationService.ReadAsync(app, parts, productIds, cancellationToken);

        return read.IsSuccess ? new StoreContentRead(MonetizationContent.AsContent(read.Snapshot)) : new StoreContentRead(null, read.ErrorKey, read.ErrorDetail);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(ChangePlanApplyRequest request, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == request.Store) ?? throw new StoreJobException($"{request.Store} has no monetization support.", transient: false);
        var changes = JsonSerializer.Deserialize<MonetizationChangeSet>(request.Changes, _json) ?? new MonetizationChangeSet();
        var outcome = await gateway.ApplyAsync(request.Target, changes, context, cancellationToken);

        if (request.Kind == ChangePlanKind.Monetization)
        {
            var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

            if (app != null)
                await _monetizationService.QueueSyncAsync(app, cancellationToken);
        }

        return outcome;
    }

    /// <inheritdoc/>
    public AuditAction AuditActionOf(ChangePlanKind kind, PlanItem item)
        => kind == ChangePlanKind.Monetization ? MonetizationRules.AuditActionOf(item.Group) : AuditAction.PurchaseSettingsChanged;
}
