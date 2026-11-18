using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The monetization part of the fake App Store Connect: subscription groups, subscriptions, In-App Purchases v2,
/// versions with v2 localizations, prices and generated price points (tier based, equalized by tier), offers, review
/// screenshots, availability, the server notification attributes of the app and sandbox testers. Resources live as
/// generic rows (type, attributes, relationships); every write is recorded in <see cref="Writes"/>.
/// </summary>
public sealed partial class FakeAppStoreConnect
{
    /// <summary>
    /// Territories of the fake with their currencies.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> MoneyTerritories = new Dictionary<string, string>
    {
        ["USA"] = "USD",
        ["TUR"] = "TRY",
        ["DEU"] = "EUR",
        ["GBR"] = "GBP",
        ["JPN"] = "JPY",
    };

    private static readonly Dictionary<string, (string ChildType, string Relation)> _lists = new()
    {
        ["apps/inAppPurchasesV2"] = ("inAppPurchases", "app"),
        ["subscriptionGroups/subscriptions"] = ("subscriptions", "group"),
        ["subscriptionGroups/versions"] = ("subscriptionGroupVersions", "subscriptionGroup"),
        ["subscriptions/versions"] = ("subscriptionVersions", "subscription"),
        ["inAppPurchases/versions"] = ("inAppPurchaseVersions", "inAppPurchase"),
        ["subscriptionGroupVersions/localizations"] = ("subscriptionGroupLocalizations", "version"),
        ["subscriptionVersions/localizations"] = ("subscriptionLocalizations", "version"),
        ["inAppPurchaseVersions/localizations"] = ("inAppPurchaseLocalizations", "version"),
        ["subscriptions/introductoryOffers"] = ("subscriptionIntroductoryOffers", "subscription"),
        ["subscriptions/promotionalOffers"] = ("subscriptionPromotionalOffers", "subscription"),
        ["subscriptions/offerCodes"] = ("subscriptionOfferCodes", "subscription"),
        ["subscriptions/winBackOffers"] = ("winBackOffers", "subscription"),
        ["subscriptionOfferCodes/customCodes"] = ("subscriptionOfferCodeCustomCodes", "offerCode"),
        ["subscriptionOfferCodes/oneTimeUseCodes"] = ("subscriptionOfferCodeOneTimeUseCodes", "offerCode"),
    };

    private static readonly Dictionary<string, (string ChildType, string Relation)> _singles = new()
    {
        ["subscriptions/appStoreReviewScreenshot"] = ("subscriptionAppStoreReviewScreenshots", "subscription"),
        ["inAppPurchases/appStoreReviewScreenshot"] = ("inAppPurchaseAppStoreReviewScreenshots", "inAppPurchaseV2"),
        ["subscriptions/subscriptionAvailability"] = ("subscriptionAvailabilities", "subscription"),
        ["inAppPurchases/inAppPurchaseAvailability"] = ("inAppPurchaseAvailabilities", "inAppPurchase"),
        ["inAppPurchases/iapPriceSchedule"] = ("inAppPurchasePriceSchedules", "inAppPurchase"),
    };

    public List<FakeRow> Rows { get; } = [];

    public List<(string Method, string Path, JsonObject Body)> Writes { get; } = [];

    /// <summary>
    /// Adds a subscription group with one subscription (en-US localization in an approved version, prices at
    /// <paramref name="tier"/> in every territory). Returns the subscription id.
    /// </summary>
    public string AddSubscription(string appId, string groupName, string productId, string state = "READY_TO_SUBMIT", int tier = 5, bool screenshot = false)
    {
        lock (_lock)
        {
            var group = Rows.FirstOrDefault(r => r.Type == "subscriptionGroups" && (string)r.Attributes["referenceName"] == groupName && r.Rels["app"] == appId)
                        ?? AddRow("subscriptionGroups", new JsonObject { ["referenceName"] = groupName }, new() { ["app"] = appId });
            var groupVersion = AddRow("subscriptionGroupVersions", new JsonObject { ["state"] = "APPROVED", ["version"] = 1 }, new() { ["subscriptionGroup"] = group.Id });

            AddRow("subscriptionGroupLocalizations", new JsonObject { ["locale"] = "en-US", ["name"] = groupName }, new() { ["version"] = groupVersion.Id });

            var subscription = AddRow("subscriptions", new JsonObject
            {
                ["productId"] = productId,
                ["name"] = productId,
                ["subscriptionPeriod"] = "ONE_MONTH",
                ["groupLevel"] = 1,
                ["state"] = state,
                ["familySharable"] = false,
            }, new() { ["group"] = group.Id });

            var version = AddRow("subscriptionVersions", new JsonObject { ["state"] = "APPROVED", ["version"] = 1 }, new() { ["subscription"] = subscription.Id });

            AddRow("subscriptionLocalizations", new JsonObject { ["locale"] = "en-US", ["name"] = "Plus", ["description"] = "Everything" }, new() { ["version"] = version.Id });

            foreach (var territory in MoneyTerritories.Keys)
                AddRow("subscriptionPrices", new JsonObject { ["startDate"] = null, ["preserved"] = false }, new() { ["subscription"] = subscription.Id, ["territory"] = territory, ["subscriptionPricePoint"] = PointId("spp", territory, tier) });

            if (screenshot)
                AddRow("subscriptionAppStoreReviewScreenshots", new JsonObject { ["fileName"] = "shot.png", ["sourceFileChecksum"] = "abc", ["assetDeliveryState"] = new JsonObject { ["state"] = "COMPLETE" } }, new() { ["subscription"] = subscription.Id });

            return subscription.Id;
        }
    }

    public void AddSandboxTester(string id, string accountName, string territory = "USA")
    {
        lock (_lock)
            Rows.Add(new FakeRow(id, "sandboxTesters", new JsonObject { ["firstName"] = "Test", ["lastName"] = "User", ["acAccountName"] = accountName, ["territory"] = territory, ["interruptPurchases"] = false, ["subscriptionRenewalRate"] = "MONTHLY_RENEWAL_EVERY_ONE_HOUR" }, []));
    }

    public IEnumerable<FakeRow> Of(string type, string relation = null, string relationId = null)
    {
        lock (_lock)
            return [.. Rows.Where(r => r.Type == type && (relation == null || (r.Rels.TryGetValue(relation, out var id) && id == relationId)))];
    }

    public static string PointId(string prefix, string territory, int tier) => $"{prefix}-{territory}-{tier}";

    public static string CustomerPrice(string territory, int tier) => territory switch
    {
        "TUR" => (tier * 10m - 0.01m).ToString(CultureInfo.InvariantCulture),
        "JPN" => (tier * 150).ToString(CultureInfo.InvariantCulture),
        _ => (tier - 0.01m).ToString(CultureInfo.InvariantCulture),
    };

    private FakeRow AddRow(string type, JsonObject attributes, Dictionary<string, string> rels)
    {
        var row = new FakeRow(NewId(type[..3]), type, attributes, rels);
        Rows.Add(row);

        return row;
    }

    /// <summary>
    /// Routes the monetization paths; null when the path is not one of them.
    /// </summary>
    private HttpResponseMessage RouteMonetization(string method, string path, System.Collections.Specialized.NameValueCollection query, JsonObject body)
    {
        Match m;

        if (method != "GET")
            Writes.Add((method, path, (JsonObject)body?.DeepClone()));

        if (method == "PATCH" && (m = Path("^/v1/apps/([^/]+)$", path)).Success)
        {
            foreach (var (name, value) in body["data"]["attributes"].AsObject())
                Apps[m.Groups[1].Value][name] = value?.DeepClone();

            return Ok(new JsonObject { ["data"] = Resource("apps", m.Groups[1].Value, Apps[m.Groups[1].Value]) });
        }

        if (method == "GET" && (m = Path("^/v1/apps/([^/]+)/subscriptionGroups$", path)).Success)
        {
            var groups = Rows.Where(r => r.Type == "subscriptionGroups" && r.Rels["app"] == m.Groups[1].Value)
                             .Where(r => query["filter[referenceName]"] == null || (string)r.Attributes["referenceName"] == query["filter[referenceName]"])
                             .ToList();
            var subscriptions = Rows.Where(r => r.Type == "subscriptions" && groups.Any(g => g.Id == r.Rels["group"])).ToList();

            return Ok(new JsonObject
            {
                ["data"] = new JsonArray([.. groups.Select(g =>
                {
                    var resource = g.ToResource();
                    resource["relationships"]["subscriptions"] = new JsonObject { ["data"] = new JsonArray([.. subscriptions.Where(s => s.Rels["group"] == g.Id).Select(s => (JsonNode)new JsonObject { ["type"] = "subscriptions", ["id"] = s.Id })]) };

                    return (JsonNode)resource;
                })]),
                ["included"] = new JsonArray([.. subscriptions.Select(s => (JsonNode)s.ToResource())]),
            });
        }

        if (method == "GET" && (m = Path("^/v[12]/([A-Za-z]+)/([^/]+)/([A-Za-z0-9]+)$", path)).Success && _lists.TryGetValue($"{m.Groups[1].Value}/{m.Groups[3].Value}", out var list))
        {
            var rows = Rows.Where(r => r.Type == list.ChildType && r.Rels.TryGetValue(list.Relation, out var parent) && parent == m.Groups[2].Value)
                           .Where(r => query["filter[productId]"] == null || (string)r.Attributes["productId"] == query["filter[productId]"])
                           .ToList();
            var included = new JsonArray([.. rows.Where(r => r.Rels.ContainsKey("territory")).Select(r => r.Rels["territory"]).Distinct().Select(t => (JsonNode)Territory(t))]);

            return Ok(new JsonObject { ["data"] = new JsonArray([.. rows.Select(r => (JsonNode)r.ToResource())]), ["included"] = included });
        }

        if (method == "GET" && (m = Path("^/v[12]/([A-Za-z]+)/([^/]+)/([A-Za-z0-9]+)$", path)).Success && _singles.TryGetValue($"{m.Groups[1].Value}/{m.Groups[3].Value}", out var single))
        {
            var row = Rows.LastOrDefault(r => r.Type == single.ChildType && r.Rels.TryGetValue(single.Relation, out var parent) && parent == m.Groups[2].Value);

            return row == null ? NotFound() : Ok(new JsonObject { ["data"] = row.ToResource() });
        }

        if (method == "GET" && Path("^/v1/subscriptions/[^/]+/retentionMessage$", path).Success)
            return NotFound();

        if (method == "GET" && (m = Path("^/v1/subscriptions/([^/]+)/prices$", path)).Success)
            return PriceList(Rows.Where(r => r.Type == "subscriptionPrices" && r.Rels["subscription"] == m.Groups[1].Value).ToList(), "subscriptionPricePoint", "spp");

        if (method == "GET" && (m = Path("^/v1/inAppPurchasePriceSchedules/([^/]+)/(manualPrices|automaticPrices)$", path)).Success)
            return IapPrices(m.Groups[1].Value, m.Groups[2].Value == "manualPrices");

        if (method == "GET" && (m = Path("^/v[12]/(subscriptions|inAppPurchases)/[^/]+/pricePoints$", path)).Success)
        {
            var prefix = m.Groups[1].Value == "subscriptions" ? "spp" : "ipp";
            var territory = query["filter[territory]"];

            return Ok(new JsonObject
            {
                ["data"] = new JsonArray([.. Enumerable.Range(1, 10).Select(tier => (JsonNode)Point(prefix, territory, tier))]),
                ["included"] = new JsonArray(Territory(territory)),
            });
        }

        if (method == "GET" && (m = Path("^/v1/(subscriptionPricePoints|inAppPurchasePricePoints)/([^/]+)(/equalizations)?$", path)).Success)
        {
            var parts = m.Groups[2].Value.Split('-');
            var tier = int.Parse(parts[2], CultureInfo.InvariantCulture);

            if (!m.Groups[3].Success)
                return Ok(new JsonObject { ["data"] = Point(parts[0], parts[1], tier), ["included"] = new JsonArray(Territory(parts[1])) });

            var others = MoneyTerritories.Keys.Where(t => t != parts[1]).ToList();

            return Ok(new JsonObject
            {
                ["data"] = new JsonArray([.. others.Select(t => (JsonNode)Point(parts[0], t, tier))]),
                ["included"] = new JsonArray([.. others.Select(t => (JsonNode)Territory(t))]),
            });
        }

        if (method == "GET" && (m = Path("^/v1/(subscriptionAvailabilities|inAppPurchaseAvailabilities)/([^/]+)/availableTerritories$", path)).Success)
        {
            var row = Rows.First(r => r.Id == m.Groups[2].Value);

            return Ok(new JsonObject { ["data"] = new JsonArray([.. ((JsonArray)row.Attributes["_availableTerritories"]).Select(t => (JsonNode)Territory((string)t))]) });
        }

        if (method == "GET" && path == "/v2/sandboxTesters")
            return Ok(new JsonObject { ["data"] = new JsonArray([.. Rows.Where(r => r.Type == "sandboxTesters").Select(r => (JsonNode)r.ToResource())]) });

        if (method == "POST" && (m = Path("^/v[12]/([A-Za-z]+)$", path)).Success && body?["data"] != null)
            return Create(m.Groups[1].Value, body);

        if (method == "PATCH" && (m = Path("^/v[12]/([A-Za-z]+)/([^/]+)$", path)).Success)
        {
            var row = Rows.FirstOrDefault(r => r.Id == m.Groups[2].Value && r.Type == m.Groups[1].Value);

            if (row == null)
                return NotFound();

            foreach (var (name, value) in body["data"]["attributes"]?.AsObject() ?? [])
                row.Attributes[name] = value?.DeepClone();

            if (row.Type.EndsWith("ReviewScreenshots", StringComparison.Ordinal) && row.Attributes["uploaded"]?.GetValue<bool>() == true)
                row.Attributes["assetDeliveryState"] = new JsonObject { ["state"] = "COMPLETE" };

            return Ok(new JsonObject { ["data"] = row.ToResource() });
        }

        if (method == "DELETE" && (m = Path("^/v[12]/([A-Za-z]+)/([^/]+)$", path)).Success)
        {
            Rows.RemoveAll(r => r.Id == m.Groups[2].Value);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return null;
    }

    private HttpResponseMessage Create(string type, JsonObject body)
    {
        var data = body["data"].AsObject();
        var attributes = (JsonObject)data["attributes"]?.DeepClone() ?? [];
        var rels = new Dictionary<string, string>();

        foreach (var (name, value) in data["relationships"]?.AsObject() ?? [])
        {
            if (value?["data"] is JsonArray array)
                attributes[$"_{name}"] = new JsonArray([.. array.Select(a => (JsonNode)(string)a["id"])]);
            else if (value?["data"]?["id"] is { } id)
                rels[name] = (string)id;
        }

        if (type is "subscriptions" or "inAppPurchases" && Rows.Any(r => r.Type == type && (string)r.Attributes["productId"] == (string)attributes["productId"]))
            return Conflict("The product id has already been used.");

        if (type is "subscriptions" or "inAppPurchases")
            attributes["state"] = "MISSING_METADATA";

        if (type.EndsWith("Versions", StringComparison.Ordinal))
        {
            attributes["state"] = "PREPARE_FOR_SUBMISSION";
            attributes["version"] = Rows.Count(r => r.Type == type && r.Rels.Values.Intersect(rels.Values).Any()) + 1;
        }

        var row = AddRow(type, attributes, rels);

        if (type.EndsWith("ReviewScreenshots", StringComparison.Ordinal))
        {
            row.Attributes["assetDeliveryState"] = new JsonObject { ["state"] = "AWAITING_UPLOAD" };
            row.Attributes["uploadOperations"] = new JsonArray(new JsonObject
            {
                ["method"] = "PUT",
                ["url"] = $"{UploadHost}/review/{row.Id}",
                ["offset"] = 0,
                ["length"] = (long)attributes["fileSize"],
                ["requestHeaders"] = new JsonArray(),
            });
        }

        foreach (var included in body["included"]?.AsArray() ?? [])
        {
            var childRels = new Dictionary<string, string> { ["parent"] = row.Id };

            foreach (var (name, value) in included["relationships"]?.AsObject() ?? [])
                childRels[name] = (string)value["data"]["id"];

            AddRow((string)included["type"], (JsonObject)included["attributes"]?.DeepClone() ?? [], childRels);
        }

        var resource = row.ToResource();

        row.Attributes.Remove("uploadOperations");

        return Ok(new JsonObject { ["data"] = resource }, HttpStatusCode.Created);
    }

    private HttpResponseMessage PriceList(List<FakeRow> prices, string pointRelation, string prefix)
    {
        var included = new JsonArray();

        foreach (var price in prices)
        {
            var parts = price.Rels[pointRelation].Split('-');

            included.Add(Point(prefix, parts[1], int.Parse(parts[2], CultureInfo.InvariantCulture)));
        }

        foreach (var territory in prices.Select(p => p.Rels["territory"]).Distinct())
            included.Add(Territory(territory));

        return Ok(new JsonObject { ["data"] = new JsonArray([.. prices.Select(p => (JsonNode)p.ToResource())]), ["included"] = included });
    }

    private HttpResponseMessage IapPrices(string scheduleId, bool manual)
    {
        var schedule = Rows.First(r => r.Id == scheduleId);
        var baseTerritory = schedule.Rels["baseTerritory"];
        var manualRows = Rows.Where(r => r.Type == "inAppPurchasePrices" && r.Rels.GetValueOrDefault("parent") == scheduleId)
                             .Select(r => (Row: r, Territory: r.Rels["inAppPurchasePricePoint"].Split('-')[1]))
                             .ToList();

        if (manual)
        {
            var rows = manualRows.Select(p => new FakeRow(p.Row.Id, "inAppPurchasePrices", new JsonObject { ["startDate"] = null, ["endDate"] = null }, new()
            {
                ["territory"] = p.Territory,
                ["inAppPurchasePricePoint"] = p.Row.Rels["inAppPurchasePricePoint"],
            })).ToList();

            return PriceList(rows, "inAppPurchasePricePoint", "ipp");
        }

        var basePoint = manualRows.First(p => p.Territory == baseTerritory).Row.Rels["inAppPurchasePricePoint"];
        var tier = int.Parse(basePoint.Split('-')[2], CultureInfo.InvariantCulture);
        var automatic = MoneyTerritories.Keys.Where(t => manualRows.All(p => p.Territory != t))
                                        .Select(t => new FakeRow($"auto-{t}", "inAppPurchasePrices", new JsonObject { ["startDate"] = null, ["endDate"] = null }, new() { ["territory"] = t, ["inAppPurchasePricePoint"] = PointId("ipp", t, tier) }))
                                        .ToList();

        return PriceList(automatic, "inAppPurchasePricePoint", "ipp");
    }

    private static JsonObject Point(string prefix, string territory, int tier) => new()
    {
        ["type"] = prefix == "spp" ? "subscriptionPricePoints" : "inAppPurchasePricePoints",
        ["id"] = PointId(prefix, territory, tier),
        ["attributes"] = new JsonObject { ["customerPrice"] = CustomerPrice(territory, tier), ["proceeds"] = "0.70" },
        ["relationships"] = new JsonObject { ["territory"] = new JsonObject { ["data"] = new JsonObject { ["type"] = "territories", ["id"] = territory } } },
    };

    private static JsonObject Territory(string territory) => new()
    {
        ["type"] = "territories",
        ["id"] = territory,
        ["attributes"] = new JsonObject { ["currency"] = MoneyTerritories.GetValueOrDefault(territory ?? string.Empty, "USD") },
    };
}

/// <summary>
/// A generic JSON:API resource of the fake.
/// </summary>
public sealed class FakeRow(string id, string type, JsonObject attributes, Dictionary<string, string> rels)
{
    public string Id { get; } = id;

    public string Type { get; } = type;

    public JsonObject Attributes { get; } = attributes;

    public Dictionary<string, string> Rels { get; } = rels;

    public JsonObject ToResource()
    {
        var attributes = new JsonObject();

        foreach (var (name, value) in Attributes.Where(a => !a.Key.StartsWith('_')))
            attributes[name] = value?.DeepClone();

        var relationships = new JsonObject();

        foreach (var (name, id) in Rels)
            relationships[name] = new JsonObject { ["data"] = new JsonObject { ["type"] = name, ["id"] = id } };

        return new JsonObject { ["type"] = Type, ["id"] = Id, ["attributes"] = attributes, ["relationships"] = relationships };
    }
}
