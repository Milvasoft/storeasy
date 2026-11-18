using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores.Content;

namespace Storeasy.Infrastructure.Stores.Monetization;

/// <summary>
/// The <see cref="StoreJobKind.SyncMonetization"/> job: reads every subscription group, subscription and In-App Purchase
/// (Play subscriptions and one-time products) of a store record with all their details and saves them, so the money
/// screens show the last read state when the store cannot be reached. A store that cannot be reached fails the step
/// transiently and the runner retries.
/// </summary>
/// <param name="contentService"></param>
/// <param name="monetizationService"></param>
public sealed class SyncMonetizationJobHandler(IStoreContentService contentService, IMonetizationService monetizationService) : IStoreJobHandler
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IMonetizationService _monetizationService = monetizationService;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.SyncMonetization;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<SyncMonetizationPayload>() ?? throw new StoreJobException("The job has no store record.", transient: false);
        var app = await _contentService.GetStoreAppAsync(context.OrganizationId, payload.StoreAppId, cancellationToken) ?? throw new StoreJobException("The store record was removed.", transient: false);

        Application.Utils.Monetization.MonetizationSnapshot snapshot = null;

        await ContentSteps.RunAsync(context, "products.read", app.Store.ToString(), async () =>
        {
            var read = await _monetizationService.ReadAsync(app, MonetizationParts.All, null, cancellationToken);

            if (!read.IsSuccess)
                throw new StoreJobException(read.ErrorDetail ?? read.ErrorKey, transient: read.ErrorKey == MessageKey.StoreUnreachable);

            snapshot = read.Snapshot;

            return $"{snapshot.Groups.Count} groups, {snapshot.Products.Count} products read.";
        }, cancellationToken, StepDirection.Inbound);

        var saved = await _monetizationService.SaveAsync(app, snapshot, cancellationToken);

        return new StoreJobOutcome($"{saved} products of store record {app.StoreAppId} synced.", new { app.StoreAppId, Groups = snapshot.Groups.Count, Products = saved });
    }
}
