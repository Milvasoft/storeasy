using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// An in-memory App Store Connect (the JSON:API paths the content engine uses), with the behaviours store-ops found in
/// practice: creating an app info localization also creates the version localization of the locale (so a later POST of
/// it answers 409), screenshots go through reserve, upload operations and commit with the MD5, and failures can be
/// injected (429/5xx answers) to test the backoff. Every request is recorded.
/// </summary>
public sealed partial class FakeAppStoreConnect
{
    public const string UploadHost = "https://asc-upload.test";

    private readonly Lock _lock = new();
    private int _sequence;

    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>
    /// Status codes to answer the next requests matching a path fragment with (for retry tests).
    /// </summary>
    public ConcurrentDictionary<string, ConcurrentQueue<HttpStatusCode>> Failures { get; } = new();

    public Dictionary<string, JsonObject> Apps { get; } = [];

    public Dictionary<string, JsonObject> Versions { get; } = [];

    public Dictionary<string, JsonObject> AppInfos { get; } = [];

    public Dictionary<string, JsonObject> InfoLocalizations { get; } = [];

    public Dictionary<string, JsonObject> VersionLocalizations { get; } = [];

    public Dictionary<string, JsonObject> ScreenshotSets { get; } = [];

    public Dictionary<string, JsonObject> Screenshots { get; } = [];

    public Dictionary<string, JsonObject> Builds { get; } = [];

    public Dictionary<string, JsonObject> ReviewDetails { get; } = [];

    public Dictionary<string, JsonObject> AgeRatings { get; } = [];

    public Dictionary<string, JsonObject> Territories { get; } = [];

    public List<string> SubmittedVersions { get; } = [];

    public Dictionary<string, byte[]> Uploads { get; } = [];

    public string NewId(string prefix) => $"{prefix}{Interlocked.Increment(ref _sequence)}";

    /// <summary>
    /// Adds an app with an editable 1.0 iOS version, an app info, one locale with complete texts, age rating, review details
    /// and availability in two territories. Returns the version id.
    /// </summary>
    public string AddApp(string appId, string locale = "en-US", bool withBuild = true)
    {
        lock (_lock)
        {
            var versionId = NewId("ver");
            var infoId = NewId("info");
            var buildId = NewId("build");

            Apps[appId] = new JsonObject { ["primaryLocale"] = locale, ["contentRightsDeclaration"] = "DOES_NOT_USE_THIRD_PARTY_CONTENT" };
            Versions[versionId] = new JsonObject
            {
                ["app"] = appId,
                ["versionString"] = "1.0",
                ["platform"] = "IOS",
                ["appVersionState"] = "PREPARE_FOR_SUBMISSION",
                ["releaseType"] = "AFTER_APPROVAL",
                ["copyright"] = "2026 Test",
                ["createdDate"] = "2026-11-01T10:00:00Z",
                ["build"] = withBuild ? buildId : null,
            };
            Builds[buildId] = new JsonObject { ["version"] = "12", ["processingState"] = "VALID", ["usesNonExemptEncryption"] = false, ["expired"] = false };
            AppInfos[infoId] = new JsonObject { ["app"] = appId, ["state"] = "PREPARE_FOR_SUBMISSION", ["primaryCategory"] = "PRODUCTIVITY", ["secondaryCategory"] = null };
            AgeRatings[infoId] = new JsonObject { ["id"] = NewId("age"), ["violenceCartoonOrFantasy"] = "NONE", ["gambling"] = false, ["unrestrictedWebAccess"] = false };

            AddLocale(appId, versionId, locale);

            ReviewDetails[versionId] = new JsonObject
            {
                ["id"] = NewId("review"),
                ["contactFirstName"] = "Ada",
                ["contactLastName"] = "Lovelace",
                ["contactEmail"] = "ada@example.test",
                ["contactPhone"] = "+905550000000",
                ["demoAccountRequired"] = false,
                ["notes"] = null,
            };

            Territories[appId + ":TUR"] = new JsonObject { ["app"] = appId, ["territory"] = "TUR", ["available"] = true };
            Territories[appId + ":CHN"] = new JsonObject { ["app"] = appId, ["territory"] = "CHN", ["available"] = false };

            return versionId;
        }
    }

    public void AddLocale(string appId, string versionId, string locale)
    {
        var infoId = AppInfos.First(i => (string)i.Value["app"] == appId).Key;

        InfoLocalizations[NewId("il")] = new JsonObject { ["info"] = infoId, ["locale"] = locale, ["name"] = "Pusula", ["subtitle"] = "Find your way", ["privacyPolicyUrl"] = "https://example.test/privacy" };
        VersionLocalizations[NewId("vl")] = new JsonObject
        {
            ["version"] = versionId,
            ["locale"] = locale,
            ["description"] = "A compass app.",
            ["keywords"] = "compass,map",
            ["supportUrl"] = "https://example.test/support",
            ["promotionalText"] = null,
            ["whatsNew"] = null,
            ["marketingUrl"] = null,
        };
    }

    public async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request)
    {
        var path = request.RequestUri.AbsolutePath;
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        var method = request.Method.Method;
        var body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync();

        Requests.Enqueue($"{method} {path}");

        foreach (var (fragment, statuses) in Failures)
        {
            if (path.Contains(fragment, StringComparison.Ordinal) && statuses.TryDequeue(out var status))
                return new HttpResponseMessage(status) { Content = new StringContent("""{"errors":[{"detail":"injected failure"}]}""") };
        }

        if (request.RequestUri.Host == new Uri(UploadHost).Host)
        {
            lock (_lock)
                Uploads[path] = body;

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        lock (_lock)
        {
            var json = body == null || body.Length == 0 ? null : JsonNode.Parse(body)?.AsObject();

            return Route(method, path, query, json);
        }
    }

    private HttpResponseMessage Route(string method, string path, System.Collections.Specialized.NameValueCollection query, JsonObject body)
    {
        Match m;

        if (method == "GET" && (m = Path("^/v1/apps/([^/]+)$", path)).Success)
            return Apps.TryGetValue(m.Groups[1].Value, out var app) ? Ok(Resource("apps", m.Groups[1].Value, app)) : NotFound();

        if (method == "GET" && (m = Path("^/v1/apps/([^/]+)/appStoreVersions$", path)).Success)
        {
            var versions = Versions.Where(v => (string)v.Value["app"] == m.Groups[1].Value).ToList();
            var data = new JsonArray([.. versions.Select(v => (JsonNode)Resource("appStoreVersions", v.Key, v.Value, new JsonObject
            {
                ["build"] = Rel("builds", (string)v.Value["build"]),
                ["appStoreVersionPhasedRelease"] = Rel("appStoreVersionPhasedReleases", null),
            }))]);
            var included = new JsonArray([.. versions.Where(v => v.Value["build"] != null).Select(v => (JsonNode)Resource("builds", (string)v.Value["build"], Builds[(string)v.Value["build"]]))]);

            return Ok(new JsonObject { ["data"] = data, ["included"] = included });
        }

        if (method == "GET" && (m = Path("^/v1/apps/([^/]+)/appInfos$", path)).Success)
            return List("appInfos", AppInfos.Where(i => (string)i.Value["app"] == m.Groups[1].Value));

        if (method == "GET" && (m = Path("^/v1/appInfos/([^/]+)/appInfoLocalizations$", path)).Success)
            return List("appInfoLocalizations", InfoLocalizations.Where(l => (string)l.Value["info"] == m.Groups[1].Value));

        if (method == "GET" && (m = Path("^/v1/appStoreVersions/([^/]+)/appStoreVersionLocalizations$", path)).Success)
            return List("appStoreVersionLocalizations", VersionLocalizations.Where(l => (string)l.Value["version"] == m.Groups[1].Value));

        if (method == "GET" && Path("^/v1/apps/[^/]+/endUserLicenseAgreement$", path).Success)
            return NotFound();

        if (method == "GET" && (m = Path("^/v1/appInfos/([^/]+)$", path)).Success)
        {
            var info = AppInfos[m.Groups[1].Value];

            return Ok(new JsonObject
            {
                ["data"] = Resource("appInfos", m.Groups[1].Value, info, new JsonObject
                {
                    ["primaryCategory"] = Rel("appCategories", (string)info["primaryCategory"]),
                    ["secondaryCategory"] = Rel("appCategories", (string)info["secondaryCategory"]),
                }),
            });
        }

        if (method == "PATCH" && (m = Path("^/v1/appInfos/([^/]+)$", path)).Success)
        {
            var info = AppInfos[m.Groups[1].Value];

            foreach (var (name, value) in body["data"]["relationships"].AsObject())
                info[name] = value?["data"]?["id"]?.GetValue<string>();

            return Ok(new JsonObject { ["data"] = Resource("appInfos", m.Groups[1].Value, info) });
        }

        if (method == "GET" && (m = Path("^/v1/appInfos/([^/]+)/ageRatingDeclaration$", path)).Success)
        {
            var rating = AgeRatings[m.Groups[1].Value];
            var attributes = (JsonObject)rating.DeepClone();
            attributes.Remove("id");

            return Ok(new JsonObject { ["data"] = Resource("ageRatingDeclarations", (string)rating["id"], attributes) });
        }

        if (method == "PATCH" && (m = Path("^/v1/ageRatingDeclarations/([^/]+)$", path)).Success)
        {
            var rating = AgeRatings.Values.First(r => (string)r["id"] == m.Groups[1].Value);

            foreach (var (name, value) in body["data"]["attributes"].AsObject())
                rating[name] = value?.DeepClone();

            return Ok(new JsonObject { ["data"] = Resource("ageRatingDeclarations", m.Groups[1].Value, new JsonObject()) });
        }

        if (method == "GET" && Path("^/v1/apps/[^/]+/appPriceSchedule$", path).Success)
            return NotFound();

        if (method == "GET" && (m = Path("^/v1/apps/([^/]+)/appAvailabilityV2$", path)).Success)
            return Ok(new JsonObject { ["data"] = Resource("appAvailabilities", "avail-" + m.Groups[1].Value, new JsonObject { ["availableInNewTerritories"] = true }) });

        if (method == "GET" && (m = Path("^/v2/appAvailabilities/avail-([^/]+)/territoryAvailabilities$", path)).Success)
        {
            var appId = m.Groups[1].Value;
            var data = new JsonArray([.. Territories.Where(t => (string)t.Value["app"] == appId).Select(t => (JsonNode)Resource("territoryAvailabilities", t.Key, new JsonObject { ["available"] = (bool)t.Value["available"] }, new JsonObject
            {
                ["territory"] = Rel("territories", (string)t.Value["territory"]),
            }))]);

            return Ok(new JsonObject { ["data"] = data });
        }

        if (method == "PATCH" && (m = Path("^/v1/territoryAvailabilities/(.+)$", path)).Success)
        {
            Territories[Uri.UnescapeDataString(m.Groups[1].Value)]["available"] = body["data"]["attributes"]["available"].GetValue<bool>();

            return Ok(new JsonObject { ["data"] = Resource("territoryAvailabilities", m.Groups[1].Value, new JsonObject()) });
        }

        return RouteContent(method, path, query, body);
    }

    private static Match Path(string pattern, string path) => Regex.Match(path, pattern);

    private static JsonObject Rel(string type, string id) => new() { ["data"] = id == null ? null : new JsonObject { ["type"] = type, ["id"] = id } };

    private static JsonObject Resource(string type, string id, JsonObject attributes, JsonObject relationships = null)
    {
        var copy = (JsonObject)attributes.DeepClone();

        foreach (var key in new[] { "app", "info", "version", "set", "build", "locId" })
            copy.Remove(key);

        var resource = new JsonObject { ["type"] = type, ["id"] = id, ["attributes"] = copy };

        if (relationships != null)
            resource["relationships"] = relationships;

        return resource;
    }

    private static HttpResponseMessage List(string type, IEnumerable<KeyValuePair<string, JsonObject>> items)
        => Ok(new JsonObject { ["data"] = new JsonArray([.. items.Select(i => (JsonNode)Resource(type, i.Key, i.Value))]) });

    private static HttpResponseMessage Ok(JsonNode body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound) { Content = new StringContent("""{"errors":[{"detail":"not found"}]}""") };

    private static HttpResponseMessage Conflict(string detail) => new(HttpStatusCode.Conflict) { Content = new StringContent(JsonSerializer.Serialize(new { errors = new[] { new { detail } } })) };

    public static string Md5(byte[] data) => Convert.ToHexStringLower(MD5.HashData(data));
}
