using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// An in-memory Google Play Developer API (androidpublisher v3) for the content engine: token exchange, edits that work on
/// a copy and change the app only on commit, details, listings, images, tracks, testers, resumable bundle uploads and
/// the Data safety form. <see cref="OpenEdits"/> shows whether an edit was left open.
/// </summary>
public sealed class FakeGooglePlay
{
    private readonly Lock _lock = new();
    private int _sequence;

    public ConcurrentQueue<string> Requests { get; } = new();

    public Dictionary<string, JsonObject> Apps { get; } = [];

    public Dictionary<string, (string Package, JsonObject State)> Edits { get; } = [];

    public Dictionary<string, string> DataSafety { get; } = [];

    public Dictionary<string, List<byte[]>> UploadSessions { get; } = [];

    public int OpenEdits
    {
        get
        {
            lock (_lock)
                return Edits.Count;
        }
    }

    public void AddApp(string package)
    {
        lock (_lock)
        {
            Apps[package] = new JsonObject
            {
                ["details"] = new JsonObject { ["contactEmail"] = "dev@example.test", ["defaultLanguage"] = "en-US" },
                ["listings"] = new JsonObject
                {
                    ["en-US"] = new JsonObject { ["language"] = "en-US", ["title"] = "Pusula", ["shortDescription"] = "Find your way", ["fullDescription"] = "A compass app." },
                },
                ["images"] = new JsonObject(),
                ["tracks"] = new JsonObject
                {
                    ["internal"] = new JsonObject { ["track"] = "internal", ["releases"] = new JsonArray(new JsonObject { ["versionCodes"] = new JsonArray("10"), ["status"] = "completed" }) },
                    ["production"] = new JsonObject
                    {
                        ["track"] = "production",
                        ["releases"] = new JsonArray(
                            new JsonObject { ["name"] = "2.4.0", ["versionCodes"] = new JsonArray("9"), ["status"] = "inProgress", ["userFraction"] = 0.2 },
                            new JsonObject { ["name"] = "2.3.0", ["versionCodes"] = new JsonArray("8"), ["status"] = "completed" }),
                    },
                    ["alpha"] = new JsonObject { ["track"] = "alpha", ["releases"] = new JsonArray() },
                },
                ["testers"] = new JsonObject { ["alpha"] = new JsonObject { ["googleGroups"] = new JsonArray("old@googlegroups.com") } },
                ["bundles"] = new JsonArray(),
            };
        }
    }

    public async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request)
    {
        var path = request.RequestUri.AbsolutePath;
        var method = request.Method.Method;
        var bytes = request.Content == null ? [] : await request.Content.ReadAsByteArrayAsync();

        Requests.Enqueue($"{method} {path}");

        if (request.RequestUri.Host == "oauth2.googleapis.com")
            return Json(new JsonObject { ["access_token"] = "ya29.fake-token", ["expires_in"] = 3600 });

        lock (_lock)
            return Route(request, method, path, bytes);
    }

    private HttpResponseMessage Route(HttpRequestMessage request, string method, string path, byte[] bytes)
    {
        Match m;
        var body = bytes.Length > 0 && request.Content?.Headers.ContentType?.MediaType == "application/json" ? JsonNode.Parse(bytes)?.AsObject() : null;

        if (method == "POST" && (m = Regex.Match(path, "^/androidpublisher/v3/applications/([^/]+)/dataSafety$")).Success)
        {
            DataSafety[m.Groups[1].Value] = (string)body["safetyLabels"];
            return Json(new JsonObject());
        }

        if (method == "POST" && (m = Regex.Match(path, "^/androidpublisher/v3/applications/([^/]+)/edits$")).Success)
        {
            if (!Apps.TryGetValue(m.Groups[1].Value, out var app))
                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":{"message":"Package not found."}}""") };

            var id = $"edit{Interlocked.Increment(ref _sequence)}";
            Edits[id] = (m.Groups[1].Value, (JsonObject)app.DeepClone());

            return Json(new JsonObject { ["id"] = id });
        }

        if ((m = Regex.Match(path, "^/(?:upload/)?androidpublisher/v3/applications/([^/]+)/edits/([^/:]+)(:commit)?(/.*)?$")).Success)
        {
            var editId = m.Groups[2].Value;

            if (!Edits.TryGetValue(editId, out var edit))
                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":{"message":"Edit not found."}}""") };

            if (m.Groups[3].Success)
            {
                Apps[edit.Package] = edit.State;
                Edits.Remove(editId);
                return Json(new JsonObject { ["id"] = editId });
            }

            if (method == "DELETE" && !m.Groups[4].Success)
            {
                Edits.Remove(editId);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return EditRoute(request, method, m.Groups[4].Value, edit.State, body, bytes);
        }

        if (method == "PUT" && UploadSessions.ContainsKey(path))
            return ResumableChunk(request, path, bytes);

        return new HttpResponseMessage(HttpStatusCode.NotImplemented) { Content = new StringContent($"Fake Play has no route for {method} {path}") };
    }

    private HttpResponseMessage EditRoute(HttpRequestMessage request, string method, string sub, JsonObject state, JsonObject body, byte[] bytes)
    {
        Match m;

        if (sub == "/details")
        {
            if (method == "PATCH")
            {
                foreach (var (name, value) in body)
                    state["details"][name] = value?.DeepClone();
            }

            return Json(state["details"].DeepClone());
        }

        if (sub == "/listings" && method == "GET")
            return Json(new JsonObject { ["listings"] = new JsonArray([.. state["listings"].AsObject().Select(l => l.Value.DeepClone())]) });

        if ((m = Regex.Match(sub, "^/listings/([^/]+)$")).Success)
        {
            var language = m.Groups[1].Value;
            var listings = state["listings"].AsObject();

            if (method == "PATCH" && !listings.ContainsKey(language))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            listings[language] ??= new JsonObject { ["language"] = language };

            foreach (var (name, value) in body ?? [])
                listings[language][name] = value?.DeepClone();

            return Json(listings[language].DeepClone());
        }

        if ((m = Regex.Match(sub, "^/listings/([^/]+)/([A-Za-z]+)$")).Success)
        {
            var key = $"{m.Groups[1].Value}/{m.Groups[2].Value}";
            var images = state["images"].AsObject();

            images[key] ??= new JsonArray();

            switch (method)
            {
                case "DELETE":
                    images[key] = new JsonArray();
                    return Json(new JsonObject());
                case "POST":
                    var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
                    var image = new JsonObject { ["id"] = $"img{Interlocked.Increment(ref _sequence)}", ["sha256"] = sha, ["url"] = $"https://play.test/{sha}" };
                    images[key].AsArray().Add(image);
                    return Json(new JsonObject { ["image"] = image.DeepClone() });
                default:
                    return Json(new JsonObject { ["images"] = images[key].DeepClone() });
            }
        }

        if (sub == "/tracks" && method == "GET")
            return Json(new JsonObject { ["tracks"] = new JsonArray([.. state["tracks"].AsObject().Select(t => t.Value.DeepClone())]) });

        if ((m = Regex.Match(sub, "^/tracks/([^/]+)$")).Success)
        {
            if (method == "PUT")
                state["tracks"][m.Groups[1].Value] = body.DeepClone();

            return state["tracks"][m.Groups[1].Value] is { } track ? Json(track.DeepClone()) : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if ((m = Regex.Match(sub, "^/testers/([^/]+)$")).Success)
        {
            if (method == "PUT")
                state["testers"][m.Groups[1].Value] = body.DeepClone();

            return state["testers"][m.Groups[1].Value] is { } testers
                ? Json(testers.DeepClone())
                : new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":{"message":"Track has no testers."}}""") };
        }

        if (sub == "/bundles" && method == "POST" && request.RequestUri.Query.Contains("uploadType=resumable"))
        {
            var session = $"/upload-session/{Interlocked.Increment(ref _sequence)}";
            UploadSessions[session] = [];

            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            response.Headers.Location = new Uri($"https://androidpublisher.googleapis.com{session}?edit={Uri.EscapeDataString(EditOf(state))}");

            return response;
        }

        return new HttpResponseMessage(HttpStatusCode.NotImplemented) { Content = new StringContent($"Fake Play edit has no route for {method} {sub}") };
    }

    private HttpResponseMessage ResumableChunk(HttpRequestMessage request, string path, byte[] bytes)
    {
        UploadSessions[path].Add(bytes);

        var range = request.Content.Headers.ContentRange;
        var received = UploadSessions[path].Sum(c => (long)c.Length);

        if (range?.Length is { } total && received < total)
        {
            var incomplete = new HttpResponseMessage((HttpStatusCode)308);
            incomplete.Headers.TryAddWithoutValidation("Range", $"bytes=0-{received - 1}");

            return incomplete;
        }

        var editId = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["edit"];
        var state = Edits[editId].State;
        var versionCode = 100 + state["bundles"].AsArray().Count;
        var sha = Convert.ToHexStringLower(SHA256.HashData([.. UploadSessions[path].SelectMany(c => c)]));

        state["bundles"].AsArray().Add(new JsonObject { ["versionCode"] = versionCode, ["sha256"] = sha });

        return Json(new JsonObject { ["versionCode"] = versionCode, ["sha256"] = sha });
    }

    private string EditOf(JsonObject state) => Edits.First(e => ReferenceEquals(e.Value.State, state)).Key;

    private static HttpResponseMessage Json(JsonNode body) => new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
