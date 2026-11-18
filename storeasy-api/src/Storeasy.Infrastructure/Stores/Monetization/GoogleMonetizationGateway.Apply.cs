using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores.Content;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Monetization;

public sealed partial class GoogleMonetizationGateway
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, MonetizationChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var outcome = new List<string>();

        foreach (var op in changes.Operations)
            outcome.Add(await ContentSteps.RunAsync(context, $"{op.Kind}.{op.ProductId}", op.Kind.ToString(), () => ApplyOneAsync(api, op, cancellationToken), cancellationToken));

        return outcome;
    }

    private Task<string> ApplyOneAsync(GooglePlayApi api, MonetizationChangeDto op, CancellationToken cancellationToken) => op.Kind switch
    {
        MonetizationChangeKind.PlaySubscription => PlaySubscriptionAsync(api, op, cancellationToken),
        MonetizationChangeKind.BasePlan => BasePlanAsync(api, op, cancellationToken),
        MonetizationChangeKind.BasePlanState => StateAsync(api, op, $"{SubscriptionPath(api, op.ProductId)}/basePlans/{Uri.EscapeDataString(op.BasePlanId)}", new JsonObject { ["basePlanId"] = op.BasePlanId }, cancellationToken),
        MonetizationChangeKind.BasePlanPrices => BasePlanPricesAsync(api, op, cancellationToken),
        MonetizationChangeKind.PlayOffer => PlayOfferAsync(api, op, cancellationToken),
        MonetizationChangeKind.PlayOfferState => StateAsync(api, op, $"{SubscriptionPath(api, op.ProductId)}/basePlans/{Uri.EscapeDataString(op.BasePlanId)}/offers/{Uri.EscapeDataString(op.Code)}", new JsonObject { ["basePlanId"] = op.BasePlanId, ["offerId"] = op.Code }, cancellationToken),
        MonetizationChangeKind.OneTimeProduct => OneTimeProductAsync(api, op, cancellationToken),
        _ => throw new StoreCallException($"{op.Kind} is not a Google Play change.", null, false),
    };

    private static async Task<string> PlaySubscriptionAsync(GooglePlayApi api, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var subscription = await GetOrNullAsync(api, SubscriptionPath(api, op.ProductId), cancellationToken);
        var version = Uri.EscapeDataString(op.RegionsVersion ?? DefaultRegionsVersion);

        if (subscription == null)
        {
            var created = new JsonObject
            {
                ["packageName"] = api.PackageName,
                ["productId"] = op.ProductId,
                ["listings"] = new JsonArray([.. (op.Localizations ?? []).Select(l => (JsonNode)Listing(l, true))]),
            };

            using var _ = await api.SendAsync(HttpMethod.Post, $"{api.AppPath}/subscriptions?productId={Uri.EscapeDataString(op.ProductId)}&regionsVersion.version={version}", created, cancellationToken);

            return $"Play subscription {op.ProductId} created.";
        }

        MergeListings(subscription, op.Localizations, true);

        using var patched = await api.SendAsync(HttpMethod.Patch, $"{SubscriptionPath(api, op.ProductId)}?updateMask=listings&regionsVersion.version={version}", subscription, cancellationToken);

        return $"Play subscription {op.ProductId}: listings updated.";
    }

    private static async Task<string> BasePlanAsync(GooglePlayApi api, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var subscription = await GetOrNullAsync(api, SubscriptionPath(api, op.ProductId), cancellationToken) ?? throw new StoreCallException($"Play subscription {op.ProductId} was not found.", 404, false);
        var plans = subscription["basePlans"] as JsonArray ?? [];

        if (plans.Any(p => (string)p?["basePlanId"] == op.BasePlanId))
            return $"Base plan {op.BasePlanId} already exists.";

        var plan = new JsonObject
        {
            ["basePlanId"] = op.BasePlanId,
            ["regionalConfigs"] = new JsonArray([.. (op.Prices ?? []).Select(p => (JsonNode)new JsonObject { ["regionCode"] = p.Territory, ["newSubscriberAvailability"] = true, ["price"] = MoneyNode(p.Currency, p.Amount) })]),
        };

        plan[op.AutoRenewing == false ? "prepaidBasePlanType" : "autoRenewingBasePlanType"] = new JsonObject { ["billingPeriodDuration"] = op.Duration };

        if (op.OtherRegionsUsd != null && op.OtherRegionsEur != null)
            plan["otherRegionsConfig"] = new JsonObject { ["usdPrice"] = MoneyNode("USD", op.OtherRegionsUsd), ["eurPrice"] = MoneyNode("EUR", op.OtherRegionsEur), ["newSubscriberAvailability"] = true };

        plans.Add(plan);
        subscription["basePlans"] = plans;

        using var _ = await api.SendAsync(HttpMethod.Patch, $"{SubscriptionPath(api, op.ProductId)}?updateMask=basePlans&regionsVersion.version={Uri.EscapeDataString(op.RegionsVersion ?? DefaultRegionsVersion)}", subscription, cancellationToken);

        return $"Base plan {op.BasePlanId} added (draft).";
    }

    private async Task<string> BasePlanPricesAsync(GooglePlayApi api, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var subscription = await GetOrNullAsync(api, SubscriptionPath(api, op.ProductId), cancellationToken) ?? throw new StoreCallException($"Play subscription {op.ProductId} was not found.", 404, false);
        var plan = (subscription["basePlans"] as JsonArray ?? []).FirstOrDefault(p => (string)p?["basePlanId"] == op.BasePlanId) as JsonObject
                   ?? throw new StoreCallException($"Base plan {op.BasePlanId} was not found.", 404, false);
        var configs = plan["regionalConfigs"] as JsonArray ?? [];
        var version = op.RegionsVersion ?? DefaultRegionsVersion;

        foreach (var price in op.Prices ?? [])
        {
            var config = configs.FirstOrDefault(c => (string)c?["regionCode"] == price.Territory) as JsonObject;

            if (config == null)
            {
                config = new JsonObject { ["regionCode"] = price.Territory, ["newSubscriberAvailability"] = true };
                configs.Add(config);
            }

            config["price"] = MoneyNode(price.Currency, price.Amount);
        }

        plan["regionalConfigs"] = configs;

        if (op.OtherRegionsUsd != null && op.OtherRegionsEur != null)
            plan["otherRegionsConfig"] = new JsonObject { ["usdPrice"] = MoneyNode("USD", op.OtherRegionsUsd), ["eurPrice"] = MoneyNode("EUR", op.OtherRegionsEur), ["newSubscriberAvailability"] = true };

        using (var _ = await api.SendAsync(HttpMethod.Patch, $"{SubscriptionPath(api, op.ProductId)}?updateMask=basePlans&regionsVersion.version={Uri.EscapeDataString(version)}", subscription, cancellationToken))
        {
            // Prices written; existing subscribers stay on their price cohort unless migrated below.
        }

        if (op.PreserveCurrentPrice != false)
            return $"{op.ProductId}:{op.BasePlanId}: {op.Prices?.Count ?? 0} regional prices set for new subscribers.";

        var now = _http.Clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var migration = new JsonObject
        {
            ["packageName"] = api.PackageName,
            ["productId"] = op.ProductId,
            ["basePlanId"] = op.BasePlanId,
            ["regionsVersion"] = new JsonObject { ["version"] = version },
            ["regionalPriceMigrations"] = new JsonArray([.. (op.Prices ?? []).Select(p => (JsonNode)new JsonObject
            {
                ["regionCode"] = p.Territory,
                ["oldestAllowedPriceVersionTime"] = now,
                ["priceIncreaseType"] = "PRICE_INCREASE_TYPE_OPT_IN",
            })]),
        };

        using var migrated = await api.SendAsync(HttpMethod.Post, $"{SubscriptionPath(api, op.ProductId)}/basePlans/{Uri.EscapeDataString(op.BasePlanId)}:migratePrices", migration, cancellationToken, retry: true);

        return $"{op.ProductId}:{op.BasePlanId}: {op.Prices?.Count ?? 0} regional prices set and existing subscribers migrated.";
    }

    private static async Task<string> PlayOfferAsync(GooglePlayApi api, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var path = $"{SubscriptionPath(api, op.ProductId)}/basePlans/{Uri.EscapeDataString(op.BasePlanId)}/offers";

        if (await GetOrNullAsync(api, $"{path}/{Uri.EscapeDataString(op.Code)}", cancellationToken) != null)
            return $"Offer {op.Code} already exists.";

        var regions = op.Territories ?? [];
        var offer = new JsonObject
        {
            ["packageName"] = api.PackageName,
            ["productId"] = op.ProductId,
            ["basePlanId"] = op.BasePlanId,
            ["offerId"] = op.Code,
            ["regionalConfigs"] = new JsonArray([.. regions.Select(r => (JsonNode)new JsonObject { ["regionCode"] = r, ["newSubscriberAvailability"] = true })]),
            ["phases"] = new JsonArray([.. (op.Phases ?? []).Select(phase => (JsonNode)new JsonObject
            {
                ["duration"] = phase.Duration,
                ["recurrenceCount"] = phase.Recurrence,
                ["regionalConfigs"] = new JsonArray([.. regions.Select(r => (JsonNode)PhaseConfig(r, phase))]),
            })]),
        };

        var eligibility = op.Eligibility?.FirstOrDefault() ?? "NewCustomers";

        if (eligibility == "NewCustomers")
            offer["targeting"] = new JsonObject { ["acquisitionRule"] = new JsonObject { ["scope"] = new JsonObject { ["thisSubscription"] = new JsonObject() } } };
        else if (eligibility == "Upgrade")
            offer["targeting"] = new JsonObject { ["upgradeRule"] = new JsonObject { ["scope"] = new JsonObject { ["anySubscriptionInApp"] = new JsonObject() } } };

        using var _ = await api.SendAsync(HttpMethod.Post, $"{path}?offerId={Uri.EscapeDataString(op.Code)}&regionsVersion.version={Uri.EscapeDataString(op.RegionsVersion ?? DefaultRegionsVersion)}", offer, cancellationToken);

        return $"Offer {op.Code} created (draft).";
    }

    private static async Task<string> OneTimeProductAsync(GooglePlayApi api, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var product = await GetOrNullAsync(api, $"{api.AppPath}/oneTimeProducts/{Uri.EscapeDataString(op.ProductId)}", cancellationToken)
                      ?? new JsonObject { ["packageName"] = api.PackageName, ["productId"] = op.ProductId, ["listings"] = new JsonArray(), ["purchaseOptions"] = new JsonArray() };

        MergeListings(product, op.Localizations, false);

        var options = product["purchaseOptions"] as JsonArray ?? [];
        var option = options.FirstOrDefault(o => (string)o?["purchaseOptionId"] == op.PurchaseOptionId) as JsonObject;

        if (option == null)
        {
            option = new JsonObject { ["purchaseOptionId"] = op.PurchaseOptionId, ["buyOption"] = new JsonObject { ["legacyCompatible"] = true }, ["regionalPricingAndAvailabilityConfigs"] = new JsonArray() };
            options.Add(option);
        }

        var configs = option["regionalPricingAndAvailabilityConfigs"] as JsonArray ?? [];

        foreach (var price in op.Prices ?? [])
        {
            var config = configs.FirstOrDefault(c => (string)c?["regionCode"] == price.Territory) as JsonObject;

            if (config == null)
            {
                config = new JsonObject { ["regionCode"] = price.Territory, ["availability"] = "AVAILABLE" };
                configs.Add(config);
            }

            config["price"] = MoneyNode(price.Currency, price.Amount);
        }

        option["regionalPricingAndAvailabilityConfigs"] = configs;
        product["purchaseOptions"] = options;
        product.Remove("regionsVersion");

        using (var _ = await api.SendAsync(HttpMethod.Patch, $"{api.AppPath}/onetimeproducts/{Uri.EscapeDataString(op.ProductId)}?allowMissing=true&updateMask=listings,purchaseOptions&regionsVersion.version={Uri.EscapeDataString(op.RegionsVersion ?? DefaultRegionsVersion)}", product, cancellationToken))
        {
            // Listings, purchase option and prices written.
        }

        if (op.Active == null)
            return $"One-time product {op.ProductId} written.";

        var request = new JsonObject { ["packageName"] = api.PackageName, ["productId"] = op.ProductId, ["purchaseOptionId"] = op.PurchaseOptionId };
        var body = new JsonObject { ["requests"] = new JsonArray(new JsonObject { [op.Active.Value ? "activatePurchaseOptionRequest" : "deactivatePurchaseOptionRequest"] = request }) };

        using var states = await api.SendAsync(HttpMethod.Post, $"{api.AppPath}/oneTimeProducts/{Uri.EscapeDataString(op.ProductId)}/purchaseOptions:batchUpdateStates", body, cancellationToken, retry: true);

        return $"One-time product {op.ProductId} written ({(op.Active.Value ? "active" : "inactive")}).";
    }

    private static async Task<string> StateAsync(GooglePlayApi api, MonetizationChangeDto op, string path, JsonObject body, CancellationToken cancellationToken)
    {
        body["packageName"] = api.PackageName;
        body["productId"] = op.ProductId;

        using var _ = await api.SendAsync(HttpMethod.Post, $"{path}:{(op.Active == true ? "activate" : "deactivate")}", body, cancellationToken, retry: true);

        return $"{path[(path.LastIndexOf('/') + 1)..]} {(op.Active == true ? "activated" : "deactivated")}.";
    }

    private static async Task<JsonObject> GetOrNullAsync(GooglePlayApi api, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await api.SendAsync(HttpMethod.Get, path, null, cancellationToken);

            return JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject;
        }
        catch (StoreCallException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    private static void MergeListings(JsonObject product, IReadOnlyList<LocalizationInputDto> localizations, bool subscription)
    {
        var listings = product["listings"] as JsonArray ?? [];

        foreach (var localization in localizations ?? [])
        {
            var existing = listings.FirstOrDefault(l => (string)l?["languageCode"] == localization.Locale);

            if (existing != null)
                listings.Remove(existing);

            listings.Add(Listing(localization, subscription));
        }

        product["listings"] = listings;
    }

    private static JsonObject Listing(LocalizationInputDto localization, bool subscription)
    {
        var listing = new JsonObject { ["languageCode"] = localization.Locale, ["title"] = localization.Name };

        if (localization.Description != null)
            listing["description"] = localization.Description;

        if (subscription && localization.Benefits != null)
            listing["benefits"] = new JsonArray([.. localization.Benefits.Select(b => (JsonNode)b)]);

        return listing;
    }

    private static JsonObject PhaseConfig(string region, OfferPhaseInputDto phase)
    {
        var config = new JsonObject { ["regionCode"] = region };

        if (phase.Free)
        {
            config["free"] = new JsonObject();
        }
        else
        {
            var price = phase.Prices?.FirstOrDefault(p => string.Equals(p.Territory, region, StringComparison.OrdinalIgnoreCase));

            config["price"] = price == null ? null : MoneyNode(price.Currency, price.Amount);
        }

        return config;
    }

    private static JsonNode MoneyNode(string currency, string amount) => JsonSerializer.SerializeToNode(Money(currency, amount));

    private static string SubscriptionPath(GooglePlayApi api, string productId) => $"{api.AppPath}/subscriptions/{Uri.EscapeDataString(productId)}";
}
