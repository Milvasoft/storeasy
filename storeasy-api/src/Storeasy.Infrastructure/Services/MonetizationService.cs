using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Monetization;
using Storeasy.Domain.Stores;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IMonetizationService"/>. The connection secret is opened for one call and dropped; store failures
/// are logged (redacted) and returned as <see cref="MessageKey.StoreUnreachable"/> (transient) or
/// <see cref="MessageKey.StoreRejected"/>. Planning reads only the touched products, asks the store for equalized price
/// points (App Store) and saves the plan through <see cref="IChangePlanService"/>, so approval, typed confirmation and the
/// apply job are the same as for content plans.
/// </summary>
/// <param name="gateways"></param>
/// <param name="connectionRepository"></param>
/// <param name="groupRepository"></param>
/// <param name="productRepository"></param>
/// <param name="planRepository"></param>
/// <param name="contentService"></param>
/// <param name="changePlanService"></param>
/// <param name="storeJobQueue"></param>
/// <param name="secretProtector"></param>
/// <param name="localizer"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class MonetizationService(IEnumerable<IMonetizationGateway> gateways,
                                        IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                        IStoreasyRepositoryBase<ProductGroup> groupRepository,
                                        IStoreasyRepositoryBase<StoreProduct> productRepository,
                                        IStoreasyRepositoryBase<ChangePlan> planRepository,
                                        IStoreContentService contentService,
                                        IChangePlanService changePlanService,
                                        IStoreJobQueue storeJobQueue,
                                        ISecretProtector secretProtector,
                                        IMilvaLocalizer localizer,
                                        TimeProvider timeProvider,
                                        IMilvaLogger logger) : IMonetizationService
{
    private readonly IEnumerable<IMonetizationGateway> _gateways = gateways;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IStoreasyRepositoryBase<ProductGroup> _groupRepository = groupRepository;
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<ChangePlan> _planRepository = planRepository;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IChangePlanService _changePlanService = changePlanService;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IMilvaLocalizer _localizer = localizer;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMilvaLogger _logger = logger;

    /// <inheritdoc/>
    public async Task<MonetizationRead> ReadAsync(StoreAppContext app, MonetizationParts parts, IReadOnlyCollection<string> productIds, CancellationToken cancellationToken)
    {
        var (gateway, target, errorKey) = await OpenAsync(app, cancellationToken);

        if (errorKey != null)
            return new MonetizationRead(null, errorKey);

        try
        {
            return new MonetizationRead(await gateway.ReadAsync(target, parts, productIds, cancellationToken));
        }
        catch (StoreCallException ex)
        {
            var (key, detail) = Failure(app, ex);

            return new MonetizationRead(null, key, detail);
        }
    }

    /// <inheritdoc/>
    public Task<Response<List<PricePointDto>>> ListPricePointsAsync(StoreAppContext app, StoreProductKind kind, string productExternalId, string territory, CancellationToken cancellationToken)
        => CallAsync(app, StoreKind.Apple, async (gateway, target) => (await gateway.ListPricePointsAsync(target, kind, productExternalId, territory, cancellationToken)).ToList(), cancellationToken);

    /// <inheritdoc/>
    public Task<Response<ConvertedPricesDto>> ConvertPricesAsync(StoreAppContext app, string currency, string amount, CancellationToken cancellationToken)
        => CallAsync(app, StoreKind.Google, (gateway, target) => gateway.ConvertPricesAsync(target, currency, amount, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public Task<Response<List<SandboxTesterDto>>> ListSandboxTestersAsync(StoreAppContext app, CancellationToken cancellationToken)
        => CallAsync(app, StoreKind.Apple, async (gateway, target) => (await gateway.ListSandboxTestersAsync(target, cancellationToken)).ToList(), cancellationToken);

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> PlanAsync(StoreAppContext app, ChangePlanKind kind, List<MonetizationChangeDto> changes, bool dryRun, string confirm, CancellationToken cancellationToken)
    {
        var unsupported = changes.FirstOrDefault(c => !MonetizationPlanner.Accepts(kind, app.Store, c.Kind));

        if (unsupported != null)
            return Response<ChangePlanDto>.Error(null, Message(MessageKey.MonetizationChangeUnsupported, unsupported.Kind.ToString()));

        var (gateway, target, errorKey) = await OpenAsync(app, cancellationToken);

        if (errorKey != null)
            return Response<ChangePlanDto>.Error(null, errorKey);

        var (parts, productIds) = MonetizationPlanner.ScopeOf(changes);

        MonetizationSnapshot snapshot;
        Dictionary<string, IReadOnlyList<PricePointDto>> equalizations;
        Dictionary<string, PricePointDto> pricePoints;
        List<SandboxTesterDto> testers;

        try
        {
            snapshot = await gateway.ReadAsync(target, parts, productIds, cancellationToken);
            (equalizations, pricePoints) = await ResolvePricePointsAsync(gateway, target, snapshot, changes, cancellationToken);
            testers = changes.Any(c => c.Kind == MonetizationChangeKind.SandboxTester) ? [.. await gateway.ListSandboxTestersAsync(target, cancellationToken)] : [];
        }
        catch (StoreCallException ex)
        {
            var (key, detail) = Failure(app, ex);

            return Response<ChangePlanDto>.Error(null, new ResponseMessage(key, detail ?? key));
        }

        var assetIds = changes.Where(c => c.AssetId != null).Select(c => c.AssetId.Value).ToList();
        var assets = await _contentService.InspectAssetsAsync(app.OrganizationId, assetIds, cancellationToken);

        var result = MonetizationPlanner.Build(new MonetizationPlanInput(app,
                                                                         snapshot,
                                                                         kind,
                                                                         changes,
                                                                         equalizations,
                                                                         pricePoints,
                                                                         assets,
                                                                         testers,
                                                                         DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime),
                                                                         _localizer));

        if (!result.IsSuccess)
            return Response<ChangePlanDto>.Error(null, Message(result.ErrorKey, result.ErrorDetail));

        return await _changePlanService.SaveAsync(app, MonetizationContent.AsContent(snapshot), result.Draft, dryRun, confirm, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> ApproveAsync(int organizationId, long planId, string confirm, IReadOnlyCollection<ChangePlanKind> kinds, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == organizationId,
                                                                projection: p => new ChangePlan { Id = p.Id, Kind = p.Kind },
                                                                cancellationToken: cancellationToken);

        if (plan == null || !kinds.Contains(plan.Kind))
            return Response<ChangePlanDto>.Error(null, MessageKey.ChangePlanNotFound);

        return await _changePlanService.ApproveAsync(organizationId, planId, confirm, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SaveAsync(StoreAppContext app, MonetizationSnapshot snapshot, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var groups = await _groupRepository.GetAllAsync(g => g.StoreAppId == app.StoreAppId && g.OrganizationId == app.OrganizationId,
                                                        projection: g => new ProductGroup { Id = g.Id, ExternalId = g.ExternalId },
                                                        cancellationToken: cancellationToken);

        var groupRows = Upsert(groups, snapshot.Groups, g => g.ExternalId, (row, read) =>
        {
            row.OrganizationId = app.OrganizationId;
            row.AppId = app.AppId;
            row.StoreAppId = app.StoreAppId;
            row.Store = app.Store;
            row.ExternalId = read.ExternalId;
            row.ReferenceName = Trim(read.ReferenceName, ProductGroup.ReferenceNameMaxLength);
            row.Localizations = read.Localizations;
            row.SyncedDate = now;
        });

        if (groupRows.Updated.Count > 0)
            await _groupRepository.UpdateAsync(groupRows.Updated, cancellationToken, g => g.ReferenceName, g => g.Localizations, g => g.SyncedDate);

        if (groupRows.Added.Count > 0)
            await _groupRepository.AddRangeAsync(groupRows.Added, cancellationToken);

        var groupIds = groupRows.Added.Concat(groupRows.Updated).ToDictionary(g => g.ExternalId, g => g.Id, StringComparer.Ordinal);

        var products = await _productRepository.GetAllAsync(p => p.StoreAppId == app.StoreAppId && p.OrganizationId == app.OrganizationId,
                                                            projection: p => new StoreProduct { Id = p.Id, ExternalId = p.ExternalId },
                                                            cancellationToken: cancellationToken);

        var productRows = Upsert(products, snapshot.Products, p => p.ExternalId, (row, read) =>
        {
            row.OrganizationId = app.OrganizationId;
            row.AppId = app.AppId;
            row.StoreAppId = app.StoreAppId;
            row.Store = app.Store;
            row.Kind = read.Kind;
            row.ExternalId = read.ExternalId;
            row.ProductId = Trim(read.ProductId, StoreProduct.ProductIdMaxLength);
            row.ReferenceName = Trim(read.ReferenceName, StoreProduct.ReferenceNameMaxLength);
            row.GroupId = read.GroupExternalId != null && groupIds.TryGetValue(read.GroupExternalId, out var groupId) ? groupId : null;
            row.GroupExternalId = read.GroupExternalId;
            row.Level = read.Level;
            row.Duration = Trim(read.Duration, StoreProduct.TextMaxLength);
            row.State = read.State;
            row.StoreState = Trim(read.StoreState, StoreProduct.TextMaxLength);
            row.FamilySharable = read.FamilySharable;
            row.ReviewNote = Trim(read.ReviewNote, StoreProduct.ReviewNoteMaxLength);
            row.ReviewScreenshot = read.ReviewScreenshot;
            row.Localizations = read.Localizations ?? [];
            row.Availability = read.Availability;
            row.BaseTerritory = read.BaseTerritory;
            row.Prices = read.Prices ?? [];
            row.Offers = read.Offers ?? [];
            row.Plans = read.Plans ?? [];
            row.RetentionMessage = Trim(read.RetentionMessage, StoreProduct.ReferenceNameMaxLength);
            row.MultiSeatStatus = Trim(read.MultiSeatStatus, StoreProduct.TextMaxLength);
            row.SyncedDate = now;
        });

        if (productRows.Updated.Count > 0)
        {
            await _productRepository.UpdateAsync(productRows.Updated,
                                                 cancellationToken,
                                                 p => p.Kind,
                                                 p => p.ProductId,
                                                 p => p.ReferenceName,
                                                 p => p.GroupId,
                                                 p => p.GroupExternalId,
                                                 p => p.Level,
                                                 p => p.Duration,
                                                 p => p.State,
                                                 p => p.StoreState,
                                                 p => p.FamilySharable,
                                                 p => p.ReviewNote,
                                                 p => p.ReviewScreenshot,
                                                 p => p.Localizations,
                                                 p => p.Availability,
                                                 p => p.BaseTerritory,
                                                 p => p.Prices,
                                                 p => p.Offers,
                                                 p => p.Plans,
                                                 p => p.RetentionMessage,
                                                 p => p.MultiSeatStatus,
                                                 p => p.SyncedDate);
        }

        if (productRows.Added.Count > 0)
            await _productRepository.AddRangeAsync(productRows.Added, cancellationToken);

        if (productRows.Removed.Count > 0)
            await _productRepository.ExecuteDeleteAsync(p => productRows.Removed.Contains(p.Id), cancellationToken: cancellationToken);

        if (groupRows.Removed.Count > 0)
            await _groupRepository.ExecuteDeleteAsync(g => groupRows.Removed.Contains(g.Id), cancellationToken: cancellationToken);

        return productRows.Added.Count + productRows.Updated.Count;
    }

    /// <inheritdoc/>
    public async Task<long> QueueSyncAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(app.OrganizationId, StoreJobKind.SyncMonetization, new SyncMonetizationPayload(app.StoreAppId), app.Store, app.AppId)], cancellationToken);

        return jobIds[0];
    }

    /// <summary>
    /// Equalized price points of the requested base prices (subscriptions) and the price points of the other requested
    /// territory prices, for the diff. Store calls only, no database.
    /// </summary>
    private static async Task<(Dictionary<string, IReadOnlyList<PricePointDto>>, Dictionary<string, PricePointDto>)> ResolvePricePointsAsync(IMonetizationGateway gateway,
                                                                                                                                             StoreContentTarget target,
                                                                                                                                             MonetizationSnapshot snapshot,
                                                                                                                                             List<MonetizationChangeDto> changes,
                                                                                                                                             CancellationToken cancellationToken)
    {
        var equalizations = new Dictionary<string, IReadOnlyList<PricePointDto>>(StringComparer.Ordinal);
        var pricePoints = new Dictionary<string, PricePointDto>(StringComparer.Ordinal);

        if (snapshot.Store != StoreKind.Apple)
            return (equalizations, pricePoints);

        foreach (var change in changes.Where(c => c.Kind is MonetizationChangeKind.BasePrice or MonetizationChangeKind.TerritoryPrice && !string.IsNullOrEmpty(c.PricePointId) && !string.IsNullOrEmpty(c.Territory)))
        {
            var product = snapshot.Product(change.ProductId);

            if (product == null || pricePoints.ContainsKey(change.PricePointId) || equalizations.ContainsKey(change.PricePointId))
                continue;

            if (change.Kind == MonetizationChangeKind.BasePrice && change.Equalize && product.Kind == StoreProductKind.Subscription)
            {
                equalizations[change.PricePointId] = await gateway.EqualizeAsync(target, product.Kind, change.PricePointId, cancellationToken);
                continue;
            }

            var points = await gateway.ListPricePointsAsync(target, product.Kind, product.ExternalId, change.Territory.Trim().ToUpperInvariant(), cancellationToken);
            var point = points.FirstOrDefault(p => p.Id == change.PricePointId);

            if (point != null)
                pricePoints[point.Id] = point;
        }

        return (equalizations, pricePoints);
    }

    private async Task<Response<T>> CallAsync<T>(StoreAppContext app, StoreKind store, Func<IMonetizationGateway, StoreContentTarget, Task<T>> call, CancellationToken cancellationToken)
    {
        if (app.Store != store)
            return Response<T>.Error(default, MessageKey.StoreOperationUnsupported);

        var (gateway, target, errorKey) = await OpenAsync(app, cancellationToken);

        if (errorKey != null)
            return Response<T>.Error(default, errorKey);

        try
        {
            return Response<T>.Success(await call(gateway, target));
        }
        catch (StoreCallException ex)
        {
            var (key, detail) = Failure(app, ex);

            return Response<T>.Error(default, new ResponseMessage(key, detail ?? key));
        }
    }

    /// <summary>
    /// The store's gateway and a target with the connection secret opened (the decrypted bytes are cleared at once).
    /// </summary>
    private async Task<(IMonetizationGateway Gateway, StoreContentTarget Target, string ErrorKey)> OpenAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == app.Store);

        if (gateway == null)
            return (null, null, MessageKey.StoreNotSupported);

        if (app.ConnectionId == null)
            return (null, null, MessageKey.StoreAppNotConnected);

        var sealedSecret = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == app.ConnectionId && c.OrganizationId == app.OrganizationId,
                                                                              projection: c => new StoreConnection { Store = c.Store, EncryptedSecret = c.EncryptedSecret, EncryptedDataKey = c.EncryptedDataKey, KeyVersion = c.KeyVersion },
                                                                              cancellationToken: cancellationToken);

        if (sealedSecret?.EncryptedSecret is not { Length: > 0 })
            return (null, null, MessageKey.StoreAppNotConnected);

        byte[] plain = null;

        try
        {
            plain = _secretProtector.Unprotect(new ProtectedSecret(sealedSecret.EncryptedSecret, sealedSecret.EncryptedDataKey, sealedSecret.KeyVersion), StoreSecretContext.Connection(app.OrganizationId, sealedSecret.Store));

            return (gateway, new StoreContentTarget(Encoding.UTF8.GetString(plain), app.ExternalId), null);
        }
        catch (CryptographicException)
        {
            return (null, null, MessageKey.StoreAppNotConnected);
        }
        finally
        {
            if (plain != null)
                CryptographicOperations.ZeroMemory(plain);
        }
    }

    private (string Key, string Detail) Failure(StoreAppContext app, StoreCallException exception)
    {
        _logger?.Warning("Monetization call of {Store} record {StoreAppId} failed: {Error}", app.Store, app.StoreAppId, SecretRedactor.Redact(exception.Message));

        return (exception.Transient ? MessageKey.StoreUnreachable : MessageKey.StoreRejected, SecretRedactor.RedactAndTrim(exception.Message, 300));
    }

    private ResponseMessage Message(string key, string detail)
    {
        string text = _localizer[key, detail];

        if (detail != null && !text.Contains(detail, StringComparison.Ordinal))
            text = $"{text} ({detail})";

        return new ResponseMessage(key, text);
    }

    /// <summary>
    /// Matches stored rows to a read by the store's id: existing rows to update, new rows to add, ids no longer in the
    /// store to delete.
    /// </summary>
    private static (List<T> Added, List<T> Updated, List<long> Removed) Upsert<T>(List<T> existing, List<T> read, Func<T, string> key, Action<T, T> fill)
        where T : Milvasoft.Core.EntityBases.Concrete.Auditing.CreationAuditableEntity<long>, new()
    {
        var byKey = existing.Where(e => key(e) != null).GroupBy(key).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var added = new List<T>();
        var updated = new List<T>();

        foreach (var item in read.Where(r => key(r) != null).DistinctBy(key))
        {
            var row = byKey.TryGetValue(key(item), out var found) ? found : new T();

            fill(row, item);
            (row.Id == 0 ? added : updated).Add(row);
        }

        var kept = read.Select(key).ToHashSet(StringComparer.Ordinal);

        return (added, updated, [.. existing.Where(e => !kept.Contains(key(e))).Select(e => e.Id)]);
    }

    private static string Trim(string value, int max) => value == null || value.Length <= max ? value : value[..max];
}
