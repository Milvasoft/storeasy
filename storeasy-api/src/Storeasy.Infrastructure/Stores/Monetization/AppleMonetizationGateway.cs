using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Monetization;
using Storeasy.Infrastructure.Stores.Content;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Monetization;

/// <summary>
/// App Store Connect monetization engine (App Store Connect API 4.4.1 paths):
/// <list type="bullet">
/// <item>Subscription groups and subscriptions (<c>/v1/subscriptionGroups</c>, <c>/v1/subscriptions</c>), In-App
/// Purchases v2 (<c>/v2/inAppPurchases</c>).</item>
/// <item>Localizations through versions: a draft version (<c>PREPARE_FOR_SUBMISSION</c>) of the group, subscription or
/// In-App Purchase holds the localizations (<c>/v2/...Localizations</c>); when there is none a version is created and the
/// localizations of the latest version are copied into it first. The pre 4.4.1 endpoints are not used.</item>
/// <item>Subscription prices per territory (<c>/v1/subscriptionPrices</c>, equalized with
/// <c>/v1/subscriptionPricePoints/{id}/equalizations</c>); In-App Purchase price schedules with a base territory.</item>
/// <item>Introductory, promotional, offer code and win-back offers; availability; review screenshots (reserve, upload,
/// commit with the MD5); retention message names and multiseat status are read.</item>
/// <item>App Store Server Notifications addresses (<c>subscriptionStatusUrl</c> attributes of the app) and sandbox
/// testers (<c>/v2/sandboxTesters</c>).</item>
/// </list>
/// </summary>
/// <param name="http"></param>
/// <param name="fileStorage"></param>
public sealed partial class AppleMonetizationGateway(StoreHttp http, IFileStorage fileStorage) : IMonetizationGateway
{
    private const string _draftState = "PREPARE_FOR_SUBMISSION";

    private readonly StoreHttp _http = http;
    private readonly IFileStorage _fileStorage = fileStorage;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Apple;

    /// <inheritdoc/>
    public async Task<MonetizationSnapshot> ReadAsync(StoreContentTarget target, MonetizationParts parts, IReadOnlyCollection<string> productIds, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var appId = Uri.EscapeDataString(target.ExternalId);
        var snapshot = new MonetizationSnapshot { Store = StoreKind.Apple, ExternalId = target.ExternalId, Parts = parts };

        using (var app = await api.GetAsync($"/v1/apps/{appId}?fields[apps]=subscriptionStatusUrl,subscriptionStatusUrlVersion,subscriptionStatusUrlForSandbox,subscriptionStatusUrlVersionForSandbox", cancellationToken))
        {
            var data = JsonRead.Get(app.RootElement, "data");

            snapshot.NotificationUrl = JsonRead.Attr(data, "subscriptionStatusUrl");
            snapshot.NotificationVersion = JsonRead.Attr(data, "subscriptionStatusUrlVersion");
            snapshot.SandboxNotificationUrl = JsonRead.Attr(data, "subscriptionStatusUrlForSandbox");
            snapshot.SandboxNotificationVersion = JsonRead.Attr(data, "subscriptionStatusUrlVersionForSandbox");
        }

        var (groups, included) = await api.GetAllAsync($"/v1/apps/{appId}/subscriptionGroups?include=subscriptions&limit=50&limit[subscriptions]=50", cancellationToken);

        foreach (var group in groups)
        {
            var groupId = JsonRead.Str(group, "id");

            snapshot.Groups.Add(new ProductGroup { Store = StoreKind.Apple, ExternalId = groupId, ReferenceName = JsonRead.Attr(group, "referenceName") });

            foreach (var subscriptionId in JsonRead.RelIds(group, "subscriptions"))
            {
                var subscription = included.FirstOrDefault(i => JsonRead.Str(i, "type") == "subscriptions" && JsonRead.Str(i, "id") == subscriptionId);

                if (subscription.ValueKind == JsonValueKind.Object)
                    snapshot.Products.Add(Subscription(subscription, groupId));
            }
        }

        var (iaps, _) = await api.GetAllAsync($"/v1/apps/{appId}/inAppPurchasesV2?limit=200", cancellationToken);

        snapshot.Products.AddRange(iaps.Select(Iap));

        if (parts.HasFlag(MonetizationParts.Localizations))
        {
            foreach (var group in snapshot.Groups)
                group.Localizations = await ReadLocalizationsAsync(api, Versioned.Group, group.ExternalId, cancellationToken);
        }

        foreach (var product in snapshot.Products.Where(p => productIds == null || productIds.Contains(p.ProductId)))
            await ReadDetailsAsync(api, product, parts, DateOnly.FromDateTime(_http.Clock.GetUtcNow().UtcDateTime), cancellationToken);

        return snapshot;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PricePointDto>> ListPricePointsAsync(StoreContentTarget target, StoreProductKind kind, string productExternalId, string territory, CancellationToken cancellationToken)
    {
        var path = kind == StoreProductKind.Subscription
            ? $"/v1/subscriptions/{Uri.EscapeDataString(productExternalId)}/pricePoints?filter[territory]={Uri.EscapeDataString(territory)}&include=territory&limit=200"
            : $"/v2/inAppPurchases/{Uri.EscapeDataString(productExternalId)}/pricePoints?filter[territory]={Uri.EscapeDataString(territory)}&include=territory&limit=200";

        var (data, included) = await Api(target).GetAllAsync(path, cancellationToken);

        return [.. data.Select(p => PricePoint(p, included))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PricePointDto>> EqualizeAsync(StoreContentTarget target, StoreProductKind kind, string pricePointId, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var type = kind == StoreProductKind.Subscription ? "subscriptionPricePoints" : "inAppPurchasePricePoints";
        var id = Uri.EscapeDataString(pricePointId);
        var points = new List<PricePointDto>();

        using (var basePoint = await api.GetAsync($"/v1/{type}/{id}?include=territory", cancellationToken))
            points.Add(PricePoint(JsonRead.Get(basePoint.RootElement, "data"), [.. JsonRead.Items(basePoint.RootElement, "included")]));

        var (data, included) = await api.GetAllAsync($"/v1/{type}/{id}/equalizations?include=territory&limit=200", cancellationToken);

        points.AddRange(data.Select(p => PricePoint(p, included)));

        return points;
    }

    /// <inheritdoc/>
    public Task<ConvertedPricesDto> ConvertPricesAsync(StoreContentTarget target, string currency, string amount, CancellationToken cancellationToken)
        => throw new StoreCallException("App Store prices are price points; there is no price conversion.", null, false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SandboxTesterDto>> ListSandboxTestersAsync(StoreContentTarget target, CancellationToken cancellationToken)
    {
        var (data, _) = await Api(target).GetAllAsync("/v2/sandboxTesters?limit=200", cancellationToken);

        return [.. data.Select(t => new SandboxTesterDto
        {
            Id = JsonRead.Str(t, "id"),
            FirstName = JsonRead.Attr(t, "firstName"),
            LastName = JsonRead.Attr(t, "lastName"),
            AccountName = JsonRead.Attr(t, "acAccountName"),
            Territory = JsonRead.Attr(t, "territory"),
            InterruptPurchases = Bool(JsonRead.Attr(t, "interruptPurchases")),
            SubscriptionRenewalRate = JsonRead.Attr(t, "subscriptionRenewalRate"),
            ApplePayCompatible = Bool(JsonRead.Attr(t, "applePayCompatible")),
        })];
    }

    private AppStoreConnectApi Api(StoreContentTarget target)
    {
        var credentials = StoreCredentialParser.ParseApple(target.SecretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

        return new AppStoreConnectApi(_http, credentials);
    }

    private static StoreProduct Subscription(JsonElement resource, string groupId)
    {
        var state = JsonRead.Attr(resource, "state");

        return new StoreProduct
        {
            Store = StoreKind.Apple,
            Kind = StoreProductKind.Subscription,
            ExternalId = JsonRead.Str(resource, "id"),
            ProductId = JsonRead.Attr(resource, "productId"),
            ReferenceName = JsonRead.Attr(resource, "name"),
            GroupExternalId = groupId,
            Level = JsonRead.Int(JsonRead.Get(resource, "attributes"), "groupLevel"),
            Duration = JsonRead.Attr(resource, "subscriptionPeriod"),
            StoreState = state,
            State = MonetizationRules.AppleState(state),
            FamilySharable = Bool(JsonRead.Attr(resource, "familySharable")),
            ReviewNote = JsonRead.Attr(resource, "reviewNote"),
            MultiSeatStatus = JsonRead.Attr(resource, "multiSeatStatus"),
        };
    }

    private static StoreProduct Iap(JsonElement resource)
    {
        var state = JsonRead.Attr(resource, "state");

        return new StoreProduct
        {
            Store = StoreKind.Apple,
            Kind = MonetizationRules.IapKind(JsonRead.Attr(resource, "inAppPurchaseType")),
            ExternalId = JsonRead.Str(resource, "id"),
            ProductId = JsonRead.Attr(resource, "productId"),
            ReferenceName = JsonRead.Attr(resource, "name"),
            StoreState = state,
            State = MonetizationRules.AppleState(state),
            FamilySharable = Bool(JsonRead.Attr(resource, "familySharable")),
            ReviewNote = JsonRead.Attr(resource, "reviewNote"),
        };
    }

    private static async Task ReadDetailsAsync(AppStoreConnectApi api, StoreProduct product, MonetizationParts parts, DateOnly today, CancellationToken cancellationToken)
    {
        var subscription = product.Kind == StoreProductKind.Subscription;
        var id = Uri.EscapeDataString(product.ExternalId);
        var root = subscription ? $"/v1/subscriptions/{id}" : $"/v2/inAppPurchases/{id}";

        if (parts.HasFlag(MonetizationParts.Localizations))
            product.Localizations = await ReadLocalizationsAsync(api, subscription ? Versioned.Subscription : Versioned.Iap, product.ExternalId, cancellationToken);

        if (parts.HasFlag(MonetizationParts.Review))
            product.ReviewScreenshot = await ReadScreenshotAsync(api, $"{root}/appStoreReviewScreenshot", cancellationToken);

        if (parts.HasFlag(MonetizationParts.Availability))
            product.Availability = await ReadAvailabilityAsync(api, subscription ? $"{root}/subscriptionAvailability" : $"{root}/inAppPurchaseAvailability", subscription ? "subscriptionAvailabilities" : "inAppPurchaseAvailabilities", cancellationToken);

        if (parts.HasFlag(MonetizationParts.Prices))
        {
            if (subscription)
                product.Prices = await ReadSubscriptionPricesAsync(api, root, today, cancellationToken);
            else
                await ReadIapPricesAsync(api, root, product, cancellationToken);
        }

        if (subscription && parts.HasFlag(MonetizationParts.Offers))
            product.Offers = await ReadOffersAsync(api, root, cancellationToken);

        if (subscription && parts.HasFlag(MonetizationParts.Products))
        {
            using var message = await api.GetOrNullAsync($"{root}/retentionMessage", cancellationToken);

            product.RetentionMessage = message == null ? null : JsonRead.Attr(JsonRead.Get(message.RootElement, "data"), "referenceName");
        }
    }

    private static async Task<List<ProductLocalization>> ReadLocalizationsAsync(AppStoreConnectApi api, Versioned resource, string parentId, CancellationToken cancellationToken)
    {
        var version = await LatestVersionAsync(api, resource, parentId, cancellationToken);

        if (version == null)
            return [];

        var (data, _) = await api.GetAllAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, resource.LocalizationsPath, Uri.EscapeDataString(version.Value.Id)) + "?limit=50", cancellationToken);

        return [.. data.Select(l => new ProductLocalization
        {
            Id = JsonRead.Str(l, "id"),
            Locale = JsonRead.Attr(l, "locale"),
            Name = JsonRead.Attr(l, "name"),
            Description = JsonRead.Attr(l, "description"),
            CustomAppName = JsonRead.Attr(l, "customAppName"),
            State = version.Value.State,
        })];
    }

    /// <summary>
    /// The draft version of a resource when it has one, otherwise the newest; null when there is no version.
    /// </summary>
    private static async Task<(string Id, string State)?> LatestVersionAsync(AppStoreConnectApi api, Versioned resource, string parentId, CancellationToken cancellationToken)
    {
        var (versions, _) = await api.GetAllAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, resource.VersionsPath, Uri.EscapeDataString(parentId)) + "?limit=50", cancellationToken);

        var chosen = versions.OrderByDescending(v => JsonRead.Attr(v, "state") == _draftState)
                             .ThenByDescending(v => JsonRead.Int(JsonRead.Get(v, "attributes"), "version") ?? 0)
                             .Select(v => (JsonRead.Str(v, "id"), JsonRead.Attr(v, "state")))
                             .FirstOrDefault();

        return chosen.Item1 == null ? null : chosen;
    }

    private static async Task<ProductScreenshot> ReadScreenshotAsync(AppStoreConnectApi api, string path, CancellationToken cancellationToken)
    {
        using var document = await api.GetOrNullAsync(path, cancellationToken);

        if (document == null)
            return null;

        var data = JsonRead.Get(document.RootElement, "data");
        var attributes = JsonRead.Get(data, "attributes");
        var image = JsonRead.Get(attributes, "imageAsset");

        return new ProductScreenshot
        {
            Id = JsonRead.Str(data, "id"),
            FileName = JsonRead.Str(attributes, "fileName"),
            Width = JsonRead.Int(image, "width"),
            Height = JsonRead.Int(image, "height"),
            Checksum = JsonRead.Str(attributes, "sourceFileChecksum"),
            State = JsonRead.Str(JsonRead.Get(attributes, "assetDeliveryState"), "state"),
        };
    }

    private static async Task<ProductAvailability> ReadAvailabilityAsync(AppStoreConnectApi api, string path, string type, CancellationToken cancellationToken)
    {
        string availabilityId;
        bool? newTerritories;

        using (var document = await api.GetOrNullAsync(path, cancellationToken))
        {
            if (document == null)
                return null;

            var data = JsonRead.Get(document.RootElement, "data");

            availabilityId = JsonRead.Str(data, "id");
            newTerritories = Bool(JsonRead.Attr(data, "availableInNewTerritories"));
        }

        var (territories, _) = await api.GetAllAsync($"/v1/{type}/{Uri.EscapeDataString(availabilityId)}/availableTerritories?limit=200", cancellationToken);

        return new ProductAvailability { Id = availabilityId, AvailableInNewTerritories = newTerritories, Territories = [.. territories.Select(t => JsonRead.Str(t, "id"))] };
    }

    private static async Task<List<ProductPrice>> ReadSubscriptionPricesAsync(AppStoreConnectApi api, string root, DateOnly today, CancellationToken cancellationToken)
    {
        var (data, included) = await api.GetAllAsync($"{root}/prices?include=subscriptionPricePoint,territory&limit=200", cancellationToken);

        var rows = data.Select(p => (Territory: JsonRead.RelId(p, "territory"), Id: JsonRead.Str(p, "id"), Start: JsonRead.Day(JsonRead.Get(p, "attributes"), "startDate"), Point: JsonRead.RelId(p, "subscriptionPricePoint")))
                       .Where(p => p.Territory != null)
                       .ToList();

        return [.. rows.GroupBy(r => r.Territory).Select(g =>
        {
            var current = g.Where(r => r.Start == null || r.Start <= today).OrderByDescending(r => r.Start ?? DateOnly.MinValue).FirstOrDefault();
            var pending = g.Where(r => r.Start > today).OrderBy(r => r.Start).FirstOrDefault();
            var point = included.FirstOrDefault(i => JsonRead.Str(i, "id") == (current.Point ?? pending.Point) && JsonRead.Str(i, "type") == "subscriptionPricePoints");
            var pendingPoint = included.FirstOrDefault(i => pending.Point != null && JsonRead.Str(i, "id") == pending.Point && JsonRead.Str(i, "type") == "subscriptionPricePoints");

            return new ProductPrice
            {
                Territory = g.Key,
                Currency = Currency(included, g.Key),
                PriceId = current.Id,
                PricePointId = current.Point,
                CustomerPrice = current.Point == null ? null : JsonRead.Attr(point, "customerPrice"),
                Proceeds = current.Point == null ? null : JsonRead.Attr(point, "proceeds"),
                Source = PriceSource.Store,
                StartDate = current.Start,
                PendingPricePointId = pending.Point,
                PendingCustomerPrice = pending.Point == null ? null : JsonRead.Attr(pendingPoint, "customerPrice"),
                PendingStartDate = pending.Start,
            };
        })];
    }

    private static async Task ReadIapPricesAsync(AppStoreConnectApi api, string root, StoreProduct product, CancellationToken cancellationToken)
    {
        string scheduleId;

        using (var schedule = await api.GetOrNullAsync($"{root}/iapPriceSchedule?include=baseTerritory", cancellationToken))
        {
            if (schedule == null)
                return;

            var data = JsonRead.Get(schedule.RootElement, "data");

            scheduleId = JsonRead.Str(data, "id");
            product.BaseTerritory = JsonRead.RelId(data, "baseTerritory");
        }

        var prices = new List<ProductPrice>();

        foreach (var (relation, source) in new[] { ("manualPrices", PriceSource.Manual), ("automaticPrices", PriceSource.Store) })
        {
            var (data, included) = await api.GetAllAsync($"/v1/inAppPurchasePriceSchedules/{Uri.EscapeDataString(scheduleId)}/{relation}?include=inAppPurchasePricePoint,territory&limit=200", cancellationToken);

            foreach (var price in data.Where(p => JsonRead.Day(JsonRead.Get(p, "attributes"), "endDate") == null))
            {
                var territory = JsonRead.RelId(price, "territory");
                var pointId = JsonRead.RelId(price, "inAppPurchasePricePoint");
                var point = included.FirstOrDefault(i => JsonRead.Str(i, "id") == pointId);

                if (territory == null || prices.Any(p => p.Territory == territory))
                    continue;

                prices.Add(new ProductPrice
                {
                    Territory = territory,
                    Currency = Currency(included, territory),
                    PriceId = JsonRead.Str(price, "id"),
                    PricePointId = pointId,
                    CustomerPrice = JsonRead.Attr(point, "customerPrice"),
                    Proceeds = JsonRead.Attr(point, "proceeds"),
                    Source = territory == product.BaseTerritory ? PriceSource.Store : source,
                    StartDate = JsonRead.Day(JsonRead.Get(price, "attributes"), "startDate"),
                });
            }
        }

        product.Prices = prices;
    }

    private static async Task<List<ProductOffer>> ReadOffersAsync(AppStoreConnectApi api, string root, CancellationToken cancellationToken)
    {
        var offers = new List<ProductOffer>();

        var (intro, introIncluded) = await api.GetAllAsync($"{root}/introductoryOffers?include=territory&limit=200", cancellationToken);

        offers.AddRange(intro.GroupBy(o => (Mode: JsonRead.Attr(o, "offerMode"), Duration: JsonRead.Attr(o, "duration"), Periods: JsonRead.Int(JsonRead.Get(o, "attributes"), "numberOfPeriods")))
                             .Select(g => new ProductOffer
                             {
                                 Kind = OfferKind.Introductory,
                                 Id = JsonRead.Str(g.First(), "id"),
                                 Mode = MonetizationRules.OfferModeOf(g.Key.Mode),
                                 Duration = g.Key.Duration,
                                 Periods = g.Key.Periods,
                                 Territories = [.. g.Select(o => JsonRead.RelId(o, "territory")).Where(t => t != null)],
                                 StartDate = JsonRead.Day(JsonRead.Get(g.First(), "attributes"), "startDate"),
                                 EndDate = JsonRead.Day(JsonRead.Get(g.First(), "attributes"), "endDate"),
                             }));

        var (promotional, _) = await api.GetAllAsync($"{root}/promotionalOffers?limit=200", cancellationToken);

        offers.AddRange(promotional.Select(o => new ProductOffer
        {
            Kind = OfferKind.Promotional,
            Id = JsonRead.Str(o, "id"),
            Name = JsonRead.Attr(o, "name"),
            Code = JsonRead.Attr(o, "offerCode"),
            Mode = MonetizationRules.OfferModeOf(JsonRead.Attr(o, "offerMode")),
            Duration = JsonRead.Attr(o, "duration"),
            Periods = JsonRead.Int(JsonRead.Get(o, "attributes"), "numberOfPeriods"),
        }));

        var (codes, _) = await api.GetAllAsync($"{root}/offerCodes?limit=200", cancellationToken);

        offers.AddRange(codes.Select(o => new ProductOffer
        {
            Kind = OfferKind.OfferCode,
            Id = JsonRead.Str(o, "id"),
            Name = JsonRead.Attr(o, "name"),
            Mode = MonetizationRules.OfferModeOf(JsonRead.Attr(o, "offerMode")),
            Duration = JsonRead.Attr(o, "duration"),
            Periods = JsonRead.Int(JsonRead.Get(o, "attributes"), "numberOfPeriods"),
            State = Bool(JsonRead.Attr(o, "active"))?.ToString(),
            Eligibility = [.. JsonRead.Items(JsonRead.Get(o, "attributes"), "customerEligibilities").Select(e => e.GetString())],
            OneTimeCodeCount = JsonRead.Int(JsonRead.Get(o, "attributes"), "totalNumberOfCodes"),
        }));

        var (winBack, _) = await api.GetAllAsync($"{root}/winBackOffers?limit=200", cancellationToken);

        offers.AddRange(winBack.Select(o => new ProductOffer
        {
            Kind = OfferKind.WinBack,
            Id = JsonRead.Str(o, "id"),
            Name = JsonRead.Attr(o, "referenceName"),
            Code = JsonRead.Attr(o, "offerId"),
            Mode = MonetizationRules.OfferModeOf(JsonRead.Attr(o, "offerMode")),
            Duration = JsonRead.Attr(o, "duration"),
            Periods = JsonRead.Int(JsonRead.Get(o, "attributes"), "periodCount"),
            StartDate = JsonRead.Day(JsonRead.Get(o, "attributes"), "startDate"),
            EndDate = JsonRead.Day(JsonRead.Get(o, "attributes"), "endDate"),
        }));

        return offers;
    }

    private static PricePointDto PricePoint(JsonElement point, IReadOnlyList<JsonElement> included)
    {
        var territory = JsonRead.RelId(point, "territory");

        return new PricePointDto
        {
            Id = JsonRead.Str(point, "id"),
            Territory = territory,
            Currency = Currency(included, territory),
            CustomerPrice = JsonRead.Attr(point, "customerPrice"),
            Proceeds = JsonRead.Attr(point, "proceeds"),
        };
    }

    private static string Currency(IEnumerable<JsonElement> included, string territory)
        => JsonRead.Attr(included.FirstOrDefault(i => JsonRead.Str(i, "type") == "territories" && JsonRead.Str(i, "id") == territory), "currency");

    private static bool? Bool(string value) => bool.TryParse(value, out var result) ? result : null;

    /// <summary>
    /// A resource whose localizations live in versions.
    /// </summary>
    /// <param name="VersionsPath">Versions of a parent (format item 0: parent id).</param>
    /// <param name="VersionType">JSON:API type of a version.</param>
    /// <param name="ParentRelation">Relationship of a version to its parent.</param>
    /// <param name="ParentType">JSON:API type of the parent.</param>
    /// <param name="LocalizationsPath">Localizations of a version (format item 0: version id).</param>
    /// <param name="LocalizationType">JSON:API type of a localization (v2 path).</param>
    private sealed record Versioned(string VersionsPath, string VersionType, string ParentRelation, string ParentType, string LocalizationsPath, string LocalizationType)
    {
        public static Versioned Group { get; } = new("/v1/subscriptionGroups/{0}/versions", "subscriptionGroupVersions", "subscriptionGroup", "subscriptionGroups", "/v1/subscriptionGroupVersions/{0}/localizations", "subscriptionGroupLocalizations");

        public static Versioned Subscription { get; } = new("/v1/subscriptions/{0}/versions", "subscriptionVersions", "subscription", "subscriptions", "/v1/subscriptionVersions/{0}/localizations", "subscriptionLocalizations");

        public static Versioned Iap { get; } = new("/v2/inAppPurchases/{0}/versions", "inAppPurchaseVersions", "inAppPurchase", "inAppPurchases", "/v1/inAppPurchaseVersions/{0}/localizations", "inAppPurchaseLocalizations");
    }
}
