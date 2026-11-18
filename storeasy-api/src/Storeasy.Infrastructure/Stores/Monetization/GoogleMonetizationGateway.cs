using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Monetization;
using Storeasy.Infrastructure.Stores.Content;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Monetization;

/// <summary>
/// Google Play monetization engine (Play Developer API v3, <c>monetization</c> resources): subscriptions with base plans
/// and offers (<c>/subscriptions</c>), regional prices and Play's price conversion (<c>pricing:convertRegionPrices</c>),
/// base plan and offer activation, price migration of existing subscribers (<c>basePlans:migratePrices</c>) and one-time
/// products with purchase options (<c>/oneTimeProducts</c>; the older <c>inappproducts</c> resource is not used). Prices
/// are what the member entered or Play converted; storeasy never computes one.
/// </summary>
/// <param name="http"></param>
public sealed partial class GoogleMonetizationGateway(StoreHttp http) : IMonetizationGateway
{
    /// <summary>
    /// Regions version used when a plan does not carry the one of a price conversion.
    /// </summary>
    public const string DefaultRegionsVersion = "2022/02";

    private const int _maxPages = 20;

    private readonly StoreHttp _http = http;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Google;

    /// <inheritdoc/>
    public async Task<MonetizationSnapshot> ReadAsync(StoreContentTarget target, MonetizationParts parts, IReadOnlyCollection<string> productIds, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var snapshot = new MonetizationSnapshot { Store = StoreKind.Google, ExternalId = target.ExternalId, Parts = parts };

        foreach (var subscription in await ListAsync(api, $"{api.AppPath}/subscriptions", "subscriptions", cancellationToken))
            snapshot.Products.Add(Subscription(subscription));

        foreach (var product in await ListAsync(api, $"{api.AppPath}/oneTimeProducts", "oneTimeProducts", cancellationToken))
            snapshot.Products.Add(OneTimeProduct(product));

        if (parts.HasFlag(MonetizationParts.Offers))
        {
            foreach (var product in snapshot.Products.Where(p => p.Kind == StoreProductKind.Subscription && (productIds == null || productIds.Contains(p.ProductId))))
            {
                foreach (var plan in product.Plans.Where(p => p.Type == "AutoRenewing"))
                {
                    var offers = await ListAsync(api, $"{api.AppPath}/subscriptions/{Uri.EscapeDataString(product.ProductId)}/basePlans/{Uri.EscapeDataString(plan.Id)}/offers", "subscriptionOffers", cancellationToken);

                    product.Offers.AddRange(offers.Select(Offer));
                }
            }
        }

        return snapshot;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PricePointDto>> ListPricePointsAsync(StoreContentTarget target, StoreProductKind kind, string productExternalId, string territory, CancellationToken cancellationToken)
        => throw new StoreCallException("Play prices are amounts, not price points.", null, false);

    /// <inheritdoc/>
    public Task<IReadOnlyList<PricePointDto>> EqualizeAsync(StoreContentTarget target, StoreProductKind kind, string pricePointId, CancellationToken cancellationToken)
        => throw new StoreCallException("Play prices are converted, not equalized.", null, false);

    /// <inheritdoc/>
    public Task<IReadOnlyList<SandboxTesterDto>> ListSandboxTestersAsync(StoreContentTarget target, CancellationToken cancellationToken)
        => throw new StoreCallException("Play license testers have no API; they are an assisted step.", null, false);

    /// <inheritdoc/>
    public async Task<ConvertedPricesDto> ConvertPricesAsync(StoreContentTarget target, string currency, string amount, CancellationToken cancellationToken)
    {
        var api = Api(target);

        using var document = await api.SendAsync(HttpMethod.Post, $"{api.AppPath}/pricing:convertRegionPrices", new { price = Money(currency, amount) }, cancellationToken, retry: true);

        var root = document.RootElement;
        var converted = JsonRead.Get(root, "convertedRegionPrices");
        var other = JsonRead.Get(root, "convertedOtherRegionsPrice");

        return new ConvertedPricesDto
        {
            Prices = converted.ValueKind == JsonValueKind.Object
                ? [.. converted.EnumerateObject().Select(p => new ConvertedPriceDto
                {
                    RegionCode = JsonRead.Str(p.Value, "regionCode") ?? p.Name,
                    Currency = JsonRead.Str(JsonRead.Get(p.Value, "price"), "currencyCode"),
                    Amount = Amount(JsonRead.Get(p.Value, "price")),
                }).OrderBy(p => p.RegionCode, StringComparer.Ordinal)]
                : [],
            OtherRegionsUsd = Amount(JsonRead.Get(other, "usdPrice")),
            OtherRegionsEur = Amount(JsonRead.Get(other, "eurPrice")),
            RegionsVersion = JsonRead.Str(JsonRead.Get(root, "regionVersion"), "version") ?? DefaultRegionsVersion,
            ConvertedDate = _http.Clock.GetUtcNow().UtcDateTime,
        };
    }

    private GooglePlayApi Api(StoreContentTarget target)
        => new(_http, StoreCredentialParser.ParseGoogle(target.SecretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false), target.ExternalId);

    private static async Task<List<JsonElement>> ListAsync(GooglePlayApi api, string path, string member, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        string token = null;

        for (var page = 0; page < _maxPages; page++)
        {
            var url = token == null ? $"{path}?pageSize=100" : $"{path}?pageSize=100&pageToken={Uri.EscapeDataString(token)}";

            using var document = await api.SendAsync(HttpMethod.Get, url, null, cancellationToken);

            items.AddRange(JsonRead.Items(document.RootElement, member).Select(e => e.Clone()));
            token = JsonRead.Str(document.RootElement, "nextPageToken");

            if (string.IsNullOrEmpty(token))
                break;
        }

        return items;
    }

    private static StoreProduct Subscription(JsonElement subscription)
    {
        var productId = JsonRead.Str(subscription, "productId");
        var plans = JsonRead.Items(subscription, "basePlans").Select(BasePlan).ToList();
        var listings = Listings(subscription);

        return new StoreProduct
        {
            Store = StoreKind.Google,
            Kind = StoreProductKind.Subscription,
            ExternalId = productId,
            ProductId = productId,
            ReferenceName = Trim(listings.FirstOrDefault()?.Name),
            Localizations = listings,
            Plans = plans,
            State = MonetizationRules.GoogleProductState(plans.Select(p => p.State)),
            StoreState = plans.Count == 0 ? "NO_BASE_PLAN" : string.Join(",", plans.Select(p => p.State.ToString().ToUpperInvariant()).Distinct()),
        };
    }

    private static StoreProduct OneTimeProduct(JsonElement product)
    {
        var productId = JsonRead.Str(product, "productId");
        var listings = Listings(product);
        var options = JsonRead.Items(product, "purchaseOptions").Select(o => new ProductPlan
        {
            Id = JsonRead.Str(o, "purchaseOptionId"),
            Type = JsonRead.Get(o, "rentOption").ValueKind == JsonValueKind.Object ? "Rent" : "Buy",
            State = MonetizationRules.GoogleState(JsonRead.Str(o, "state")),
            Prices = [.. JsonRead.Items(o, "regionalPricingAndAvailabilityConfigs").Select(c => Price(JsonRead.Str(c, "regionCode"), JsonRead.Get(c, "price")))],
        }).ToList();

        return new StoreProduct
        {
            Store = StoreKind.Google,
            Kind = StoreProductKind.OneTimeProduct,
            ExternalId = productId,
            ProductId = productId,
            ReferenceName = Trim(listings.FirstOrDefault()?.Name),
            Localizations = listings,
            Plans = options,
            State = MonetizationRules.GoogleProductState(options.Select(p => p.State)),
            StoreState = options.Count == 0 ? "NO_PURCHASE_OPTION" : string.Join(",", options.Select(p => p.State.ToString().ToUpperInvariant()).Distinct()),
        };
    }

    private static ProductPlan BasePlan(JsonElement plan)
    {
        var autoRenewing = JsonRead.Get(plan, "autoRenewingBasePlanType");
        var prepaid = JsonRead.Get(plan, "prepaidBasePlanType");
        var installments = JsonRead.Get(plan, "installmentsBasePlanType");
        var other = JsonRead.Get(plan, "otherRegionsConfig");

        var (type, period) = autoRenewing.ValueKind == JsonValueKind.Object ? ("AutoRenewing", JsonRead.Str(autoRenewing, "billingPeriodDuration"))
            : prepaid.ValueKind == JsonValueKind.Object ? ("Prepaid", JsonRead.Str(prepaid, "billingPeriodDuration"))
            : ("Installments", JsonRead.Str(installments, "billingPeriodDuration"));

        return new ProductPlan
        {
            Id = JsonRead.Str(plan, "basePlanId"),
            Type = type,
            BillingPeriod = period,
            State = MonetizationRules.GoogleState(JsonRead.Str(plan, "state")),
            Prices = [.. JsonRead.Items(plan, "regionalConfigs").Select(c => Price(JsonRead.Str(c, "regionCode"), JsonRead.Get(c, "price")))],
            OtherRegionsUsd = Amount(JsonRead.Get(other, "usdPrice")),
            OtherRegionsEur = Amount(JsonRead.Get(other, "eurPrice")),
        };
    }

    private static ProductOffer Offer(JsonElement offer)
    {
        var targeting = JsonRead.Get(offer, "targeting");
        var eligibility = JsonRead.Get(targeting, "acquisitionRule").ValueKind == JsonValueKind.Object ? "NewCustomers"
            : JsonRead.Get(targeting, "upgradeRule").ValueKind == JsonValueKind.Object ? "Upgrade"
            : "DeveloperDetermined";

        return new ProductOffer
        {
            Kind = OfferKind.PlayOffer,
            Id = JsonRead.Str(offer, "offerId"),
            Code = JsonRead.Str(offer, "offerId"),
            BasePlanId = JsonRead.Str(offer, "basePlanId"),
            State = JsonRead.Str(offer, "state"),
            Eligibility = [eligibility],
            Territories = [.. JsonRead.Items(offer, "regionalConfigs").Select(c => JsonRead.Str(c, "regionCode"))],
            Phases = [.. JsonRead.Items(offer, "phases").Select(p =>
            {
                var free = JsonRead.Items(p, "regionalConfigs").Any(c => JsonRead.Get(c, "free").ValueKind == JsonValueKind.Object);

                return string.Create(CultureInfo.InvariantCulture, $"{(free ? "FreeTrial" : "Price")} {JsonRead.Str(p, "duration")} x{JsonRead.Int(p, "recurrenceCount") ?? 1}");
            })],
        };
    }

    private static List<ProductLocalization> Listings(JsonElement product)
        => [.. JsonRead.Items(product, "listings").Select(l => new ProductLocalization
        {
            Locale = JsonRead.Str(l, "languageCode"),
            Name = JsonRead.Str(l, "title"),
            Description = JsonRead.Str(l, "description"),
            Benefits = JsonRead.Get(l, "benefits").ValueKind == JsonValueKind.Array ? [.. JsonRead.Items(l, "benefits").Select(b => b.GetString())] : null,
        })];

    private static ProductPrice Price(string region, JsonElement money) => new()
    {
        Territory = region,
        Currency = JsonRead.Str(money, "currencyCode"),
        CustomerPrice = Amount(money),
        Source = PriceSource.Manual,
    };

    /// <summary>
    /// Amount text of a Play Money ("59.99"), or null.
    /// </summary>
    private static string Amount(JsonElement money)
    {
        if (money.ValueKind != JsonValueKind.Object)
            return null;

        var units = decimal.TryParse(JsonRead.Str(money, "units") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) ? whole : 0;
        var nanos = JsonRead.Int(money, "nanos") ?? 0;

        return (units + (nanos / 1_000_000_000m)).ToString("0.##########", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A Play Money of an amount text.
    /// </summary>
    private static object Money(string currency, string amount)
    {
        MonetizationRules.TryParseAmount(amount, out var value);

        var units = decimal.Truncate(value);

        return new { currencyCode = currency, units = units.ToString(CultureInfo.InvariantCulture), nanos = (int)((value - units) * 1_000_000_000m) };
    }

    private static string Trim(string value) => value?.Length > StoreProduct.ReferenceNameMaxLength ? value[..StoreProduct.ReferenceNameMaxLength] : value;
}
