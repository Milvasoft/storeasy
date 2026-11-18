using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Monetization;
using Storeasy.Infrastructure.Stores.Content;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Monetization;

public sealed partial class AppleMonetizationGateway
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, MonetizationChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var run = new ApplyRun(Api(target), target.ExternalId);
        var outcome = new List<string>();

        foreach (var op in changes.Operations)
        {
            var name = $"{op.Kind}.{op.ProductId ?? op.GroupReferenceName ?? op.TesterId ?? "app"}";

            outcome.Add(await ContentSteps.RunAsync(context, name, op.Kind.ToString(), () => ApplyOneAsync(run, op, cancellationToken), cancellationToken));
        }

        return outcome;
    }

    private Task<string> ApplyOneAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken) => op.Kind switch
    {
        MonetizationChangeKind.CreateGroup => CreateGroupAsync(run, op, cancellationToken),
        MonetizationChangeKind.UpdateGroup => PatchAsync(run, $"/v1/subscriptionGroups/{op.GroupExternalId}", "subscriptionGroups", op.GroupExternalId, new JsonObject { ["referenceName"] = op.ReferenceName }, cancellationToken),
        MonetizationChangeKind.GroupLocalization => GroupLocalizationAsync(run, op, cancellationToken),
        MonetizationChangeKind.CreateSubscription => CreateSubscriptionAsync(run, op, cancellationToken),
        MonetizationChangeKind.CreateIap => CreateIapAsync(run, op, cancellationToken),
        MonetizationChangeKind.UpdateProduct or MonetizationChangeKind.Level => UpdateProductAsync(run, op, cancellationToken),
        MonetizationChangeKind.ProductLocalization => ProductLocalizationAsync(run, op, cancellationToken),
        MonetizationChangeKind.BasePrice or MonetizationChangeKind.TerritoryPrice => op.ProductKind == StoreProductKind.Subscription ? SubscriptionPricesAsync(run, op, cancellationToken) : IapPriceScheduleAsync(run, op, cancellationToken),
        MonetizationChangeKind.Availability => AvailabilityAsync(run, op, cancellationToken),
        MonetizationChangeKind.IntroductoryOffer => IntroductoryOfferAsync(run, op, cancellationToken),
        MonetizationChangeKind.PromotionalOffer => PromotionalOfferAsync(run, op, cancellationToken),
        MonetizationChangeKind.OfferCode => OfferCodeAsync(run, op, cancellationToken),
        MonetizationChangeKind.WinBackOffer => WinBackOfferAsync(run, op, cancellationToken),
        MonetizationChangeKind.ReviewScreenshot => ReviewScreenshotAsync(run, op, cancellationToken),
        MonetizationChangeKind.ServerNotifications => ServerNotificationsAsync(run, op, cancellationToken),
        MonetizationChangeKind.SandboxTester => SandboxTesterAsync(run, op, cancellationToken),
        _ => throw new StoreCallException($"{op.Kind} is not an App Store change.", null, false),
    };

    #region Groups and products

    private static async Task<string> CreateGroupAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var groupId = await FindGroupAsync(run, op.GroupReferenceName, cancellationToken);

        if (groupId == null)
        {
            using var created = await run.Api.PostAsync("/v1/subscriptionGroups", Body("subscriptionGroups", new JsonObject { ["referenceName"] = op.GroupReferenceName }, new JsonObject { ["app"] = Rel("apps", run.AppId) }), cancellationToken);

            groupId = JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
        }

        run.Groups[op.GroupReferenceName] = groupId;

        foreach (var localization in op.Localizations ?? [])
            await UpsertLocalizationAsync(run.Api, Versioned.Group, groupId, localization.Locale, new JsonObject { ["name"] = localization.Name, ["customAppName"] = localization.CustomAppName }, cancellationToken);

        return $"Subscription group {op.GroupReferenceName} ({groupId}).";
    }

    private static async Task<string> GroupLocalizationAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var groupId = op.GroupExternalId ?? await GroupIdAsync(run, op.GroupReferenceName, cancellationToken);

        await UpsertLocalizationAsync(run.Api, Versioned.Group, groupId, op.Locale, new JsonObject { ["name"] = op.Name, ["customAppName"] = op.CustomAppName }, cancellationToken);

        return $"Group localization {op.Locale} written.";
    }

    private static async Task<string> CreateSubscriptionAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var groupId = op.GroupExternalId ?? await GroupIdAsync(run, op.GroupReferenceName, cancellationToken);
        var (existing, _) = await run.Api.GetAllAsync($"/v1/subscriptionGroups/{Uri.EscapeDataString(groupId)}/subscriptions?filter[productId]={Uri.EscapeDataString(op.ProductId)}", cancellationToken);
        var id = existing.Select(s => JsonRead.Str(s, "id")).FirstOrDefault();

        if (id == null)
        {
            var attributes = new JsonObject
            {
                ["name"] = op.ReferenceName,
                ["productId"] = op.ProductId,
                ["subscriptionPeriod"] = op.Duration,
                ["familySharable"] = op.FamilySharable ?? false,
            };

            if (op.Level != null)
                attributes["groupLevel"] = op.Level;

            if (op.ReviewNote != null)
                attributes["reviewNote"] = op.ReviewNote;

            using var created = await run.Api.PostAsync("/v1/subscriptions", Body("subscriptions", attributes, new JsonObject { ["group"] = Rel("subscriptionGroups", groupId) }), cancellationToken);

            id = JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
        }

        run.Products[op.ProductId] = id;

        return $"Subscription {op.ProductId} ({id}).";
    }

    private static async Task<string> CreateIapAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await FindIapAsync(run, op.ProductId, cancellationToken);

        if (id == null)
        {
            var attributes = new JsonObject
            {
                ["name"] = op.ReferenceName,
                ["productId"] = op.ProductId,
                ["inAppPurchaseType"] = MonetizationRules.IapType(op.ProductKind ?? StoreProductKind.NonConsumable),
            };

            if (op.FamilySharable != null)
                attributes["familySharable"] = op.FamilySharable;

            if (op.ReviewNote != null)
                attributes["reviewNote"] = op.ReviewNote;

            using var created = await run.Api.PostAsync("/v2/inAppPurchases", Body("inAppPurchases", attributes, new JsonObject { ["app"] = Rel("apps", run.AppId) }), cancellationToken);

            id = JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
        }

        run.Products[op.ProductId] = id;

        return $"In-App Purchase {op.ProductId} ({id}).";
    }

    private static async Task<string> UpdateProductAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var subscription = op.ProductKind is null or StoreProductKind.Subscription;
        var id = await ProductIdAsync(run, op, cancellationToken);
        var attributes = new JsonObject();

        if (op.ReferenceName != null)
            attributes["name"] = op.ReferenceName;

        if (op.FamilySharable != null)
            attributes["familySharable"] = op.FamilySharable;

        if (op.ReviewNote != null)
            attributes["reviewNote"] = op.ReviewNote;

        if (subscription && op.Level != null)
            attributes["groupLevel"] = op.Level;

        if (subscription && op.MultiSeatStatus != null)
            attributes["multiSeatStatus"] = op.MultiSeatStatus;

        return await PatchAsync(run, subscription ? $"/v1/subscriptions/{id}" : $"/v2/inAppPurchases/{id}", subscription ? "subscriptions" : "inAppPurchases", id, attributes, cancellationToken);
    }

    private static async Task<string> ProductLocalizationAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);
        var resource = op.ProductKind == StoreProductKind.Subscription ? Versioned.Subscription : Versioned.Iap;

        await UpsertLocalizationAsync(run.Api, resource, id, op.Locale, new JsonObject { ["name"] = op.Name, ["description"] = op.Description }, cancellationToken);

        return $"{op.ProductId} localization {op.Locale} written.";
    }

    private static async Task<string> PatchAsync(ApplyRun run, string path, string type, string id, JsonObject attributes, CancellationToken cancellationToken)
    {
        using var _ = await run.Api.PatchAsync(path, new JsonObject { ["data"] = new JsonObject { ["type"] = type, ["id"] = id, ["attributes"] = attributes } }, cancellationToken);

        return $"{type} {id} updated.";
    }

    #endregion

    #region Prices and availability

    private static async Task<string> SubscriptionPricesAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);
        var (existing, _) = await run.Api.GetAllAsync($"/v1/subscriptions/{Uri.EscapeDataString(id)}/prices?limit=200", cancellationToken);
        var written = 0;

        foreach (var price in op.Prices ?? [])
        {
            var already = existing.Any(p => JsonRead.RelId(p, "subscriptionPricePoint") == price.PricePointId
                                            && (op.StartDate == null || JsonRead.Day(JsonRead.Get(p, "attributes"), "startDate") == op.StartDate));

            if (already)
                continue;

            var attributes = new JsonObject { ["startDate"] = op.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };

            if (op.PreserveCurrentPrice != null)
                attributes["preserveCurrentPrice"] = op.PreserveCurrentPrice;

            using var _ = await run.Api.PostAsync("/v1/subscriptionPrices", Body("subscriptionPrices", attributes, new JsonObject
            {
                ["subscription"] = Rel("subscriptions", id),
                ["subscriptionPricePoint"] = Rel("subscriptionPricePoints", price.PricePointId),
                ["territory"] = Rel("territories", price.Territory),
            }), cancellationToken);

            written++;
        }

        return $"{op.ProductId}: {written} territory prices scheduled.";
    }

    private static async Task<string> IapPriceScheduleAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);
        var prices = new List<(string Id, string PricePointId)> { ("${base}", op.PricePointId) };

        prices.AddRange((op.Prices ?? []).Select((p, i) => ($"${{price{i}}}", p.PricePointId)));

        var included = new JsonArray([.. prices.Select(p => (JsonNode)new JsonObject
        {
            ["type"] = "inAppPurchasePrices",
            ["id"] = p.Id,
            ["attributes"] = new JsonObject { ["startDate"] = op.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            ["relationships"] = new JsonObject { ["inAppPurchasePricePoint"] = Rel("inAppPurchasePricePoints", p.PricePointId) },
        })]);

        var body = Body("inAppPurchasePriceSchedules", null, new JsonObject
        {
            ["inAppPurchase"] = Rel("inAppPurchases", id),
            ["baseTerritory"] = Rel("territories", op.Territory),
            ["manualPrices"] = RelMany("inAppPurchasePrices", prices.Select(p => p.Id)),
        });

        body["included"] = included;

        using var _ = await run.Api.PostAsync("/v1/inAppPurchasePriceSchedules", body, cancellationToken, retry: true);

        return $"{op.ProductId}: price schedule set ({op.Territory}, {prices.Count - 1} manual territories).";
    }

    private static async Task<string> AvailabilityAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);
        var subscription = op.ProductKind == StoreProductKind.Subscription;

        using var _ = await run.Api.PostAsync(subscription ? "/v1/subscriptionAvailabilities" : "/v1/inAppPurchaseAvailabilities",
                                              Body(subscription ? "subscriptionAvailabilities" : "inAppPurchaseAvailabilities",
                                                   new JsonObject { ["availableInNewTerritories"] = op.AvailableInNewTerritories ?? true },
                                                   new JsonObject
                                                   {
                                                       [subscription ? "subscription" : "inAppPurchase"] = Rel(subscription ? "subscriptions" : "inAppPurchases", id),
                                                       ["availableTerritories"] = RelMany("territories", op.Territories ?? []),
                                                   }),
                                              cancellationToken,
                                              retry: true);

        return $"{op.ProductId}: available in {op.Territories?.Count ?? 0} territories.";
    }

    #endregion

    #region Offers

    private static async Task<string> IntroductoryOfferAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);
        var (existing, _) = await run.Api.GetAllAsync($"/v1/subscriptions/{Uri.EscapeDataString(id)}/introductoryOffers?include=territory&limit=200", cancellationToken);
        var taken = existing.Select(o => JsonRead.RelId(o, "territory")).Where(t => t != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var written = 0;

        foreach (var territory in op.Territories ?? [])
        {
            if (taken.Contains(territory))
                continue;

            var relationships = new JsonObject
            {
                ["subscription"] = Rel("subscriptions", id),
                ["territory"] = Rel("territories", territory),
            };

            var point = op.Prices?.FirstOrDefault(p => string.Equals(p.Territory, territory, StringComparison.OrdinalIgnoreCase))?.PricePointId;

            if (point != null)
                relationships["subscriptionPricePoint"] = Rel("subscriptionPricePoints", point);

            using var _ = await run.Api.PostAsync("/v1/subscriptionIntroductoryOffers", Body("subscriptionIntroductoryOffers", OfferAttributes(op, "numberOfPeriods"), relationships), cancellationToken);

            written++;
        }

        return $"{op.ProductId}: introductory offer in {written} territories.";
    }

    private static async Task<string> PromotionalOfferAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);
        var (existing, _) = await run.Api.GetAllAsync($"/v1/subscriptions/{Uri.EscapeDataString(id)}/promotionalOffers?limit=200", cancellationToken);

        if (existing.Any(o => JsonRead.Attr(o, "offerCode") == op.Code))
            return $"{op.ProductId}: promotional offer {op.Code} already exists.";

        var attributes = OfferAttributes(op, "numberOfPeriods");
        attributes["name"] = op.ReferenceName;
        attributes["offerCode"] = op.Code;

        using var _ = await run.Api.PostAsync("/v1/subscriptionPromotionalOffers", WithPrices(Body("subscriptionPromotionalOffers", attributes, new JsonObject { ["subscription"] = Rel("subscriptions", id) }), "subscriptionPromotionalOfferPrices", op.Prices), cancellationToken);

        return $"{op.ProductId}: promotional offer {op.Code} created.";
    }

    private static async Task<string> OfferCodeAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);

        if (op.Active == false)
        {
            using var deactivated = await run.Api.PatchAsync($"/v1/subscriptionOfferCodes/{op.Code}", new JsonObject
            {
                ["data"] = new JsonObject { ["type"] = "subscriptionOfferCodes", ["id"] = op.Code, ["attributes"] = new JsonObject { ["active"] = false } },
            }, cancellationToken);

            return $"{op.ProductId}: offer code {op.ReferenceName} deactivated.";
        }

        var (existing, _) = await run.Api.GetAllAsync($"/v1/subscriptions/{Uri.EscapeDataString(id)}/offerCodes?limit=200", cancellationToken);
        var offerId = existing.Where(o => JsonRead.Attr(o, "name") == op.ReferenceName).Select(o => JsonRead.Str(o, "id")).FirstOrDefault();

        if (offerId == null)
        {
            var attributes = OfferAttributes(op, "numberOfPeriods");
            attributes["name"] = op.ReferenceName;
            attributes["customerEligibilities"] = new JsonArray([.. (op.Eligibility ?? []).Select(e => (JsonNode)e)]);
            attributes["offerEligibility"] = op.OfferEligibility;

            if (op.AutoRenew != null)
                attributes["autoRenewEnabled"] = op.AutoRenew;

            using var created = await run.Api.PostAsync("/v1/subscriptionOfferCodes", WithPrices(Body("subscriptionOfferCodes", attributes, new JsonObject { ["subscription"] = Rel("subscriptions", id) }), "subscriptionOfferCodePrices", op.Prices), cancellationToken);

            offerId = JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
        }

        var expiry = op.CodeExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (op.Code != null)
        {
            var (custom, _) = await run.Api.GetAllAsync($"/v1/subscriptionOfferCodes/{Uri.EscapeDataString(offerId)}/customCodes?limit=200", cancellationToken);

            if (!custom.Any(c => JsonRead.Attr(c, "customCode") == op.Code))
            {
                using var _ = await run.Api.PostAsync("/v1/subscriptionOfferCodeCustomCodes", Body("subscriptionOfferCodeCustomCodes",
                                                                                                    new JsonObject { ["customCode"] = op.Code, ["numberOfCodes"] = op.CustomCodeCount, ["expirationDate"] = expiry },
                                                                                                    new JsonObject { ["offerCode"] = Rel("subscriptionOfferCodes", offerId) }), cancellationToken);
            }
        }

        if (op.OneTimeCodeCount > 0)
        {
            var (batches, _) = await run.Api.GetAllAsync($"/v1/subscriptionOfferCodes/{Uri.EscapeDataString(offerId)}/oneTimeUseCodes?limit=200", cancellationToken);

            if (!batches.Any(b => JsonRead.Int(JsonRead.Get(b, "attributes"), "numberOfCodes") == op.OneTimeCodeCount))
            {
                using var _ = await run.Api.PostAsync("/v1/subscriptionOfferCodeOneTimeUseCodes", Body("subscriptionOfferCodeOneTimeUseCodes",
                                                                                                        new JsonObject { ["numberOfCodes"] = op.OneTimeCodeCount, ["expirationDate"] = expiry },
                                                                                                        new JsonObject { ["offerCode"] = Rel("subscriptionOfferCodes", offerId) }), cancellationToken);
            }
        }

        return $"{op.ProductId}: offer code {op.ReferenceName} ({offerId}).";
    }

    private static async Task<string> WinBackOfferAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);
        var (existing, _) = await run.Api.GetAllAsync($"/v1/subscriptions/{Uri.EscapeDataString(id)}/winBackOffers?limit=200", cancellationToken);

        if (existing.Any(o => JsonRead.Attr(o, "offerId") == op.Code))
            return $"{op.ProductId}: win-back offer {op.Code} already exists.";

        var attributes = OfferAttributes(op, "periodCount");
        attributes["referenceName"] = op.ReferenceName;
        attributes["offerId"] = op.Code;
        attributes["customerEligibilityPaidSubscriptionDurationInMonths"] = op.PaidMonths;
        attributes["customerEligibilityTimeSinceLastSubscribedInMonths"] = new JsonObject { ["minimum"] = op.SinceMinMonths, ["maximum"] = op.SinceMaxMonths };
        attributes["priority"] = op.Priority;
        attributes["promotionIntent"] = "NOT_PROMOTED";

        if (op.WaitMonths != null)
            attributes["customerEligibilityWaitBetweenOffersInMonths"] = op.WaitMonths;

        using var _ = await run.Api.PostAsync("/v1/winBackOffers", WithPrices(Body("winBackOffers", attributes, new JsonObject { ["subscription"] = Rel("subscriptions", id) }), "winBackOfferPrices", op.Prices), cancellationToken);

        return $"{op.ProductId}: win-back offer {op.Code} created.";
    }

    private static JsonObject OfferAttributes(MonetizationChangeDto op, string periodsName)
    {
        var attributes = new JsonObject
        {
            ["offerMode"] = MonetizationRules.AppleOfferMode(op.Mode ?? OfferMode.FreeTrial),
            ["duration"] = op.Duration,
            [periodsName] = op.Periods ?? 1,
        };

        if (op.StartDate != null)
            attributes["startDate"] = op.StartDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (op.EndDate != null)
            attributes["endDate"] = op.EndDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return attributes;
    }

    private static JsonObject WithPrices(JsonObject body, string priceType, IReadOnlyList<PriceInputDto> prices)
    {
        var ids = (prices ?? []).Select((_, i) => $"${{price{i}}}").ToList();

        body["data"]["relationships"]["prices"] = RelMany(priceType, ids);
        body["included"] = new JsonArray([.. (prices ?? []).Select((p, i) => (JsonNode)new JsonObject
        {
            ["type"] = priceType,
            ["id"] = ids[i],
            ["relationships"] = new JsonObject
            {
                ["territory"] = Rel("territories", p.Territory),
                ["subscriptionPricePoint"] = Rel("subscriptionPricePoints", p.PricePointId),
            },
        })]);

        return body;
    }

    #endregion

    #region Review, settings

    private async Task<string> ReviewScreenshotAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var id = await ProductIdAsync(run, op, cancellationToken);
        var subscription = op.ProductKind == StoreProductKind.Subscription;
        var current = await ReadScreenshotAsync(run.Api, subscription ? $"/v1/subscriptions/{id}/appStoreReviewScreenshot" : $"/v2/inAppPurchases/{id}/appStoreReviewScreenshot", cancellationToken);

        if (current?.Checksum == op.Md5 && current.State is "COMPLETE" or "UPLOAD_COMPLETE")
            return $"{op.ProductId}: review screenshot already uploaded.";

        var path = subscription ? "/v1/subscriptionAppStoreReviewScreenshots" : "/v1/inAppPurchaseAppStoreReviewScreenshots";
        var type = subscription ? "subscriptionAppStoreReviewScreenshots" : "inAppPurchaseAppStoreReviewScreenshots";

        if (current?.Id != null)
            await run.Api.DeleteAsync($"{path}/{current.Id}", cancellationToken);

        var data = await ContentSteps.ReadObjectAsync(_fileStorage, op.ObjectKey, cancellationToken);

        using var reserved = await run.Api.PostAsync(path, Body(type, new JsonObject { ["fileName"] = op.FileName, ["fileSize"] = data.Length }, new JsonObject
        {
            [subscription ? "subscription" : "inAppPurchaseV2"] = Rel(subscription ? "subscriptions" : "inAppPurchases", id),
        }), cancellationToken);

        var reservation = JsonRead.Get(reserved.RootElement, "data");
        var screenshotId = JsonRead.Str(reservation, "id");

        try
        {
            await run.Api.UploadAsync(reservation, data, cancellationToken);
        }
        catch (StoreCallException)
        {
            await run.Api.DeleteAsync($"{path}/{screenshotId}", cancellationToken);
            throw;
        }

        using var committed = await run.Api.PatchAsync($"{path}/{screenshotId}", new JsonObject
        {
            ["data"] = new JsonObject { ["type"] = type, ["id"] = screenshotId, ["attributes"] = new JsonObject { ["uploaded"] = true, ["sourceFileChecksum"] = op.Md5 } },
        }, cancellationToken);

        return $"{op.ProductId}: review screenshot {op.FileName} uploaded.";
    }

    private static async Task<string> ServerNotificationsAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var attributes = new JsonObject();

        if (op.Url != null)
        {
            attributes["subscriptionStatusUrl"] = op.Url;
            attributes["subscriptionStatusUrlVersion"] = op.NotificationVersion;
        }

        if (op.SandboxUrl != null)
        {
            attributes["subscriptionStatusUrlForSandbox"] = op.SandboxUrl;
            attributes["subscriptionStatusUrlVersionForSandbox"] = op.NotificationVersion;
        }

        await PatchAsync(run, $"/v1/apps/{run.AppId}", "apps", run.AppId, attributes, cancellationToken);

        return "App Store Server Notifications addresses updated.";
    }

    private static async Task<string> SandboxTesterAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        var attributes = new JsonObject();

        if (op.Territory != null)
            attributes["territory"] = op.Territory;

        if (op.InterruptPurchases != null)
            attributes["interruptPurchases"] = op.InterruptPurchases;

        if (op.RenewalRate != null)
            attributes["subscriptionRenewalRate"] = op.RenewalRate;

        if (attributes.Count > 0)
            await PatchAsync(run, $"/v2/sandboxTesters/{op.TesterId}", "sandboxTesters", op.TesterId, attributes, cancellationToken);

        if (op.ClearPurchaseHistory == true)
        {
            using var _ = await run.Api.PostAsync("/v2/sandboxTestersClearPurchaseHistoryRequest", Body("sandboxTestersClearPurchaseHistoryRequest", null, new JsonObject
            {
                ["sandboxTesters"] = RelMany("sandboxTesters", [op.TesterId]),
            }), cancellationToken, retry: true);
        }

        return $"Sandbox tester {op.TesterId} updated.";
    }

    #endregion

    #region Lookups and helpers

    /// <summary>
    /// Writes a localization into the draft version of a resource: the draft is reused, or created with the
    /// localizations of the newest version copied first; an existing locale is patched, a new one created.
    /// </summary>
    private static async Task UpsertLocalizationAsync(AppStoreConnectApi api, Versioned resource, string parentId, string locale, JsonObject attributes, CancellationToken cancellationToken)
    {
        var versionId = await DraftVersionAsync(api, resource, parentId, cancellationToken);
        var (localizations, _) = await api.GetAllAsync(string.Format(CultureInfo.InvariantCulture, resource.LocalizationsPath, Uri.EscapeDataString(versionId)) + "?limit=50", cancellationToken);
        var existing = localizations.FirstOrDefault(l => JsonRead.Attr(l, "locale") == locale);

        foreach (var (name, value) in attributes.ToList())
        {
            if (value == null)
                attributes.Remove(name);
        }

        if (existing.ValueKind == JsonValueKind.Object)
        {
            var id = JsonRead.Str(existing, "id");

            using var _ = await api.PatchAsync($"/v2/{resource.LocalizationType}/{id}", new JsonObject { ["data"] = new JsonObject { ["type"] = resource.LocalizationType, ["id"] = id, ["attributes"] = attributes } }, cancellationToken);

            return;
        }

        attributes["locale"] = locale;

        using var created = await api.PostAsync($"/v2/{resource.LocalizationType}", Body(resource.LocalizationType, attributes, new JsonObject { ["version"] = Rel(resource.VersionType, versionId) }), cancellationToken);
    }

    private static async Task<string> DraftVersionAsync(AppStoreConnectApi api, Versioned resource, string parentId, CancellationToken cancellationToken)
    {
        var latest = await LatestVersionAsync(api, resource, parentId, cancellationToken);

        if (latest?.State == _draftState)
            return latest.Value.Id;

        using var created = await api.PostAsync($"/v1/{resource.VersionType}", Body(resource.VersionType, null, new JsonObject { [resource.ParentRelation] = Rel(resource.ParentType, parentId) }), cancellationToken);

        var versionId = JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");

        if (latest == null)
            return versionId;

        var (previous, _) = await api.GetAllAsync(string.Format(CultureInfo.InvariantCulture, resource.LocalizationsPath, Uri.EscapeDataString(latest.Value.Id)) + "?limit=50", cancellationToken);
        var (copied, _) = await api.GetAllAsync(string.Format(CultureInfo.InvariantCulture, resource.LocalizationsPath, Uri.EscapeDataString(versionId)) + "?limit=50", cancellationToken);
        var present = copied.Select(l => JsonRead.Attr(l, "locale")).ToHashSet(StringComparer.Ordinal);

        foreach (var localization in previous.Where(l => !present.Contains(JsonRead.Attr(l, "locale"))))
        {
            var attributes = new JsonObject { ["locale"] = JsonRead.Attr(localization, "locale"), ["name"] = JsonRead.Attr(localization, "name") };

            foreach (var name in new[] { "description", "customAppName" })
            {
                var value = JsonRead.Attr(localization, name);

                if (value != null)
                    attributes[name] = value;
            }

            using var _ = await api.PostAsync($"/v2/{resource.LocalizationType}", Body(resource.LocalizationType, attributes, new JsonObject { ["version"] = Rel(resource.VersionType, versionId) }), cancellationToken);
        }

        return versionId;
    }

    private static async Task<string> FindGroupAsync(ApplyRun run, string referenceName, CancellationToken cancellationToken)
    {
        var (groups, _) = await run.Api.GetAllAsync($"/v1/apps/{Uri.EscapeDataString(run.AppId)}/subscriptionGroups?filter[referenceName]={Uri.EscapeDataString(referenceName)}&limit=50", cancellationToken);

        return groups.Where(g => JsonRead.Attr(g, "referenceName") == referenceName).Select(g => JsonRead.Str(g, "id")).FirstOrDefault();
    }

    private static async Task<string> GroupIdAsync(ApplyRun run, string referenceName, CancellationToken cancellationToken)
    {
        if (run.Groups.TryGetValue(referenceName ?? string.Empty, out var id))
            return id;

        return await FindGroupAsync(run, referenceName, cancellationToken) ?? throw new StoreCallException($"Subscription group {referenceName} was not found in the store.", null, false);
    }

    private static async Task<string> FindIapAsync(ApplyRun run, string productId, CancellationToken cancellationToken)
    {
        var (iaps, _) = await run.Api.GetAllAsync($"/v1/apps/{Uri.EscapeDataString(run.AppId)}/inAppPurchasesV2?filter[productId]={Uri.EscapeDataString(productId)}&limit=50", cancellationToken);

        return iaps.Where(i => JsonRead.Attr(i, "productId") == productId).Select(i => JsonRead.Str(i, "id")).FirstOrDefault();
    }

    /// <summary>
    /// The store id of the product of an operation: planned, created earlier in this run, or looked up by product id.
    /// </summary>
    private static async Task<string> ProductIdAsync(ApplyRun run, MonetizationChangeDto op, CancellationToken cancellationToken)
    {
        if (op.ProductExternalId != null)
            return op.ProductExternalId;

        if (run.Products.TryGetValue(op.ProductId ?? string.Empty, out var id))
            return id;

        if (op.ProductKind != StoreProductKind.Subscription)
            id = await FindIapAsync(run, op.ProductId, cancellationToken);

        if (id == null)
        {
            var (groups, included) = await run.Api.GetAllAsync($"/v1/apps/{Uri.EscapeDataString(run.AppId)}/subscriptionGroups?include=subscriptions&limit=50&limit[subscriptions]=50", cancellationToken);

            id = included.Where(i => JsonRead.Str(i, "type") == "subscriptions" && JsonRead.Attr(i, "productId") == op.ProductId).Select(i => JsonRead.Str(i, "id")).FirstOrDefault();
        }

        return id ?? throw new StoreCallException($"Product {op.ProductId} was not found in the store.", null, false);
    }

    private static JsonObject Body(string type, JsonObject attributes, JsonObject relationships)
    {
        var data = new JsonObject { ["type"] = type };

        if (attributes != null)
            data["attributes"] = attributes;

        if (relationships != null)
            data["relationships"] = relationships;

        return new JsonObject { ["data"] = data };
    }

    private static JsonObject Rel(string type, string id) => new() { ["data"] = new JsonObject { ["type"] = type, ["id"] = id } };

    private static JsonObject RelMany(string type, IEnumerable<string> ids) => new() { ["data"] = new JsonArray([.. ids.Select(id => (JsonNode)new JsonObject { ["type"] = type, ["id"] = id })]) };

    /// <summary>
    /// State of one apply run: store ids of groups and products created by earlier operations.
    /// </summary>
    private sealed record ApplyRun(AppStoreConnectApi Api, string AppId)
    {
        public Dictionary<string, string> Groups { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Products { get; } = new(StringComparer.Ordinal);
    }

    #endregion
}
