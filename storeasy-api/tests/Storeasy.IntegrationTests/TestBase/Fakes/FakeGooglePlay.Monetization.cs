using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The monetization part of the fake Play Developer API: subscriptions with base plans and offers, activation, price
/// migration, one-time products with purchase options and the price conversion (every region gets the amount times
/// two, in USD). Writes are recorded in <see cref="MoneyWrites"/>.
/// </summary>
public sealed partial class FakeGooglePlay
{
    private const string _app = "^/androidpublisher/v3/applications/([^/]+)";

    public Dictionary<string, JsonObject> Subscriptions { get; } = [];

    public Dictionary<string, JsonObject> Offers { get; } = [];

    public Dictionary<string, JsonObject> OneTimeProducts { get; } = [];

    public List<(string Method, string Path, JsonObject Body)> MoneyWrites { get; } = [];

    /// <summary>
    /// Adds an active subscription with one active monthly base plan priced in TR and US.
    /// </summary>
    public void AddSubscription(string package, string productId, string basePlanId = "monthly")
    {
        lock (_lock)
        {
            Subscriptions[$"{package}/{productId}"] = new JsonObject
            {
                ["packageName"] = package,
                ["productId"] = productId,
                ["listings"] = new JsonArray(new JsonObject { ["languageCode"] = "en-US", ["title"] = "Premium", ["benefits"] = new JsonArray("Everything") }),
                ["basePlans"] = new JsonArray(new JsonObject
                {
                    ["basePlanId"] = basePlanId,
                    ["state"] = "ACTIVE",
                    ["autoRenewingBasePlanType"] = new JsonObject { ["billingPeriodDuration"] = "P1M" },
                    ["regionalConfigs"] = new JsonArray(
                        new JsonObject { ["regionCode"] = "TR", ["newSubscriberAvailability"] = true, ["price"] = new JsonObject { ["currencyCode"] = "TRY", ["units"] = "59", ["nanos"] = 990000000 } },
                        new JsonObject { ["regionCode"] = "US", ["newSubscriberAvailability"] = true, ["price"] = new JsonObject { ["currencyCode"] = "USD", ["units"] = "1", ["nanos"] = 990000000 } }),
                }),
            };
        }
    }

    private HttpResponseMessage RouteMonetization(string method, string path, string query, JsonObject body)
    {
        Match m;

        if (method != "GET" && Regex.IsMatch(path, _app + "/(subscriptions|oneTimeProducts|onetimeproducts|pricing)"))
            MoneyWrites.Add((method, path + query, (JsonObject)body?.DeepClone()));

        if (method == "POST" && (m = Regex.Match(path, _app + "/pricing:convertRegionPrices$")).Success)
        {
            var units = decimal.Parse((string)body["price"]["units"], System.Globalization.CultureInfo.InvariantCulture) + ((int?)body["price"]["nanos"] ?? 0) / 1_000_000_000m;
            var converted = new JsonObject();

            foreach (var (region, currency) in new[] { ("US", "USD"), ("DE", "EUR"), ("IN", "INR") })
                converted[region] = new JsonObject { ["regionCode"] = region, ["price"] = Money(currency, units * 2) };

            return Json(new JsonObject
            {
                ["convertedRegionPrices"] = converted,
                ["convertedOtherRegionsPrice"] = new JsonObject { ["usdPrice"] = Money("USD", 1.99m), ["eurPrice"] = Money("EUR", 1.99m) },
                ["regionVersion"] = new JsonObject { ["version"] = "2025/03" },
            });
        }

        if (method == "GET" && (m = Regex.Match(path, _app + "/subscriptions$")).Success)
            return Json(new JsonObject { ["subscriptions"] = new JsonArray([.. Subscriptions.Where(s => s.Key.StartsWith(m.Groups[1].Value + "/", StringComparison.Ordinal)).Select(s => s.Value.DeepClone())]) });

        if (method == "POST" && (m = Regex.Match(path, _app + "/subscriptions$")).Success)
        {
            var created = (JsonObject)body.DeepClone();
            created["basePlans"] ??= new JsonArray();
            Subscriptions[$"{m.Groups[1].Value}/{(string)body["productId"]}"] = created;

            return Json(created.DeepClone());
        }

        if ((m = Regex.Match(path, _app + "/subscriptions/([^/:]+)$")).Success)
        {
            var key = $"{m.Groups[1].Value}/{m.Groups[2].Value}";

            if (method == "GET")
                return Subscriptions.TryGetValue(key, out var found) ? Json(found.DeepClone()) : NotFoundJson();

            if (method == "PATCH")
            {
                var updated = (JsonObject)body.DeepClone();

                foreach (var plan in updated["basePlans"]?.AsArray() ?? [])
                    plan["state"] ??= "DRAFT";

                Subscriptions[key] = updated;

                return Json(updated.DeepClone());
            }
        }

        if (method == "POST" && (m = Regex.Match(path, _app + "/subscriptions/([^/]+)/basePlans/([^/:]+):(activate|deactivate|migratePrices)$")).Success)
        {
            var plan = Subscriptions[$"{m.Groups[1].Value}/{m.Groups[2].Value}"]["basePlans"].AsArray().First(p => (string)p["basePlanId"] == m.Groups[3].Value);

            if (m.Groups[4].Value != "migratePrices")
                plan["state"] = m.Groups[4].Value == "activate" ? "ACTIVE" : "INACTIVE";

            return Json(new JsonObject());
        }

        if ((m = Regex.Match(path, _app + "/subscriptions/([^/]+)/basePlans/([^/]+)/offers$")).Success)
        {
            var prefix = $"{m.Groups[1].Value}/{m.Groups[2].Value}/{m.Groups[3].Value}/";

            if (method == "GET")
                return Json(new JsonObject { ["subscriptionOffers"] = new JsonArray([.. Offers.Where(o => o.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(o => o.Value.DeepClone())]) });

            var offer = (JsonObject)body.DeepClone();
            offer["state"] = "DRAFT";
            Offers[prefix + (string)body["offerId"]] = offer;

            return Json(offer.DeepClone());
        }

        if ((m = Regex.Match(path, _app + "/subscriptions/([^/]+)/basePlans/([^/]+)/offers/([^/:]+)(:activate|:deactivate)?$")).Success)
        {
            var key = $"{m.Groups[1].Value}/{m.Groups[2].Value}/{m.Groups[3].Value}/{m.Groups[4].Value}";

            if (!Offers.TryGetValue(key, out var offer))
                return NotFoundJson();

            if (m.Groups[5].Success)
                offer["state"] = m.Groups[5].Value == ":activate" ? "ACTIVE" : "INACTIVE";

            return Json(offer.DeepClone());
        }

        if (method == "GET" && (m = Regex.Match(path, _app + "/oneTimeProducts$")).Success)
            return Json(new JsonObject { ["oneTimeProducts"] = new JsonArray([.. OneTimeProducts.Where(s => s.Key.StartsWith(m.Groups[1].Value + "/", StringComparison.Ordinal)).Select(s => s.Value.DeepClone())]) });

        if (method == "GET" && (m = Regex.Match(path, _app + "/oneTimeProducts/([^/:]+)$")).Success)
            return OneTimeProducts.TryGetValue($"{m.Groups[1].Value}/{m.Groups[2].Value}", out var product) ? Json(product.DeepClone()) : NotFoundJson();

        if (method == "PATCH" && (m = Regex.Match(path, _app + "/onetimeproducts/([^/:]+)$")).Success)
        {
            var key = $"{m.Groups[1].Value}/{m.Groups[2].Value}";
            var updated = (JsonObject)body.DeepClone();
            var previous = OneTimeProducts.GetValueOrDefault(key);

            foreach (var option in updated["purchaseOptions"]?.AsArray() ?? [])
            {
                var old = previous?["purchaseOptions"]?.AsArray().FirstOrDefault(o => (string)o["purchaseOptionId"] == (string)option["purchaseOptionId"]);
                option["state"] = (string)old?["state"] ?? "DRAFT";
            }

            OneTimeProducts[key] = updated;

            return Json(updated.DeepClone());
        }

        if (method == "POST" && (m = Regex.Match(path, _app + "/oneTimeProducts/([^/]+)/purchaseOptions:batchUpdateStates$")).Success)
        {
            var product = OneTimeProducts[$"{m.Groups[1].Value}/{m.Groups[2].Value}"];

            foreach (var request in body["requests"].AsArray())
            {
                var activate = request["activatePurchaseOptionRequest"];
                var target = activate ?? request["deactivatePurchaseOptionRequest"];
                var option = product["purchaseOptions"].AsArray().First(o => (string)o["purchaseOptionId"] == (string)target["purchaseOptionId"]);

                option["state"] = activate != null ? "ACTIVE" : "INACTIVE";
            }

            return Json(new JsonObject());
        }

        return null;
    }

    private static JsonObject Money(string currency, decimal amount)
    {
        var units = decimal.Truncate(amount);

        return new JsonObject { ["currencyCode"] = currency, ["units"] = units.ToString(System.Globalization.CultureInfo.InvariantCulture), ["nanos"] = (int)((amount - units) * 1_000_000_000m) };
    }

    private static HttpResponseMessage NotFoundJson() => new(HttpStatusCode.NotFound) { Content = new System.Net.Http.StringContent("""{"error":{"message":"Not found."}}""") };
}
