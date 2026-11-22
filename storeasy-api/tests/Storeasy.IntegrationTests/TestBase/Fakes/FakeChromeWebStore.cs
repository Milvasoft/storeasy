using Storeasy.Application.Utils.Chrome;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// An in-memory Chrome Web Store API v2 and Google token endpoint behind the Chrome client's HTTP handler: access tokens for
/// OAuth refresh tokens and service account assertions, items under publishers with a published and a submitted revision,
/// package uploads (manifest.json read from the ZIP, version checked), publish, deploy percentage, cancel, rollback,
/// reviews, rating summary and metrics. Tests move a submission through the review with <see cref="Approve"/> and
/// <see cref="Reject"/>. Nothing leaves the process; each test uses its own publisher and item ids.
/// </summary>
public sealed class FakeChromeWebStore : HttpMessageHandler
{
    /// <summary>API base address the tests configure.</summary>
    public const string ApiBaseUrl = "https://chromewebstore.fake";

    /// <summary>OAuth token endpoint the tests configure.</summary>
    public const string TokenUrl = "https://oauth2.fake/token";

    private readonly object _gate = new();

    /// <summary>A revision.</summary>
    public sealed class Revision
    {
        public string State { get; set; }

        public string Version { get; set; }

        public int? DeployPercentage { get; set; }
    }

    /// <summary>An item.</summary>
    public sealed class Item
    {
        public string PublisherId { get; set; }

        public string Owner { get; set; }

        public Revision Published { get; set; }

        public Revision Submitted { get; set; }

        public string UploadedVersion { get; set; }

        public string LastUploadState { get; set; }

        public bool Warned { get; set; }

        public bool TakenDown { get; set; }

        public List<string> PublishedVersions { get; } = [];

        public List<JsonObject> Reviews { get; } = [];

        public JsonObject RatingSummary { get; set; }

        public List<JsonObject> MetricRows { get; } = [];

        public List<JsonObject> PublishBodies { get; } = [];

        public long UsersLast7Days { get; set; }
    }

    /// <summary>Refresh tokens and service account emails the token endpoint accepts, with the identity they stand for.</summary>
    public ConcurrentDictionary<string, string> Accounts { get; } = new();

    /// <summary>Items by extension id.</summary>
    public ConcurrentDictionary<string, Item> Items { get; } = new();

    /// <summary>Every call: "METHOD path".</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>When set, uploads answer IN_PROGRESS once and the status shows SUCCEEDED afterwards.</summary>
    public bool UploadsInProgress { get; set; }

    private readonly ConcurrentDictionary<string, string> _tokens = new();

    /// <summary>
    /// A new item of a new publisher, owned by a new OAuth account; returns its ids and the refresh token.
    /// </summary>
    public (string PublisherId, string ItemId, string RefreshToken) AddItem(string publishedVersion = "1.0.0", int deployPercentage = 100)
    {
        var publisherId = $"pub-{Guid.NewGuid():N}"[..20];
        var refreshToken = $"refresh-{Guid.NewGuid():N}";
        var itemId = NewItemId();

        Accounts[refreshToken] = refreshToken;
        Items[itemId] = new Item
        {
            PublisherId = publisherId,
            Owner = refreshToken,
            Published = publishedVersion == null ? null : new Revision { State = "PUBLISHED", Version = publishedVersion, DeployPercentage = deployPercentage },
        };

        if (publishedVersion != null)
            Items[itemId].PublishedVersions.Add(publishedVersion);

        return (publisherId, itemId, refreshToken);
    }

    /// <summary>
    /// A new extension id (32 letters a to p).
    /// </summary>
    public static string NewItemId()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        var builder = new StringBuilder();

        foreach (var b in bytes)
            builder.Append((char)('a' + (b >> 4))).Append((char)('a' + (b & 0xF)));

        return builder.ToString();
    }

    /// <summary>The review approves the submission: published unless it was staged.</summary>
    public void Approve(string itemId)
    {
        lock (_gate)
        {
            var item = Items[itemId];

            if (item.Submitted?.State == "STAGED")
                return;

            Publish(item, item.Submitted);
        }
    }

    /// <summary>The review stages the submission (submitted with STAGED_PUBLISH).</summary>
    public void Stage(string itemId)
    {
        lock (_gate)
        {
            Items[itemId].Submitted.State = "STAGED";
        }
    }

    /// <summary>The review rejects the submission.</summary>
    public void Reject(string itemId)
    {
        lock (_gate)
        {
            Items[itemId].Submitted.State = "REJECTED";
        }
    }

    /// <summary>
    /// A ZIP with the given manifest.json.
    /// </summary>
    public static byte[] Package(string version, int manifestVersion = 3, string name = "Kabuk Tabs", bool withIcon = true, string extraManifest = null)
    {
        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var icons = withIcon ? ",\"icons\":{\"128\":\"icon128.png\"}" : string.Empty;
            var entry = archive.CreateEntry("manifest.json");

            using (var writer = new StreamWriter(entry.Open()))
                writer.Write($"{{\"manifest_version\":{manifestVersion},\"name\":\"{name}\",\"version\":\"{version}\",\"description\":\"Tabs, tidy.\"{icons}{extraManifest}}}");

            using (var writer = new StreamWriter(archive.CreateEntry("background.js").Open()))
                writer.Write("chrome.runtime.onInstalled.addListener(() => {});");
        }

        return memory.ToArray();
    }

    private static void Publish(Item item, Revision revision)
    {
        item.Published = new Revision { State = "PUBLISHED", Version = revision.Version, DeployPercentage = revision.DeployPercentage ?? item.Published?.DeployPercentage ?? 100 };
        item.PublishedVersions.Add(revision.Version);
        item.Submitted = null;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        Calls.Enqueue($"{request.Method} {Uri.UnescapeDataString(path)}");

        if (path.EndsWith("/token", StringComparison.Ordinal))
            return await TokenAsync(request, cancellationToken);

        var token = request.Headers.Authorization?.Parameter;

        if (token == null || !_tokens.TryGetValue(token, out var identity))
            return Error(HttpStatusCode.Unauthorized, "Request had invalid authentication credentials.");

        var parts = Uri.UnescapeDataString(path).Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (parts.Count > 0 && parts[0] == "upload")
            parts.RemoveAt(0);

        // v2/publishers/{p}/items/{id}:verb  or  v2/publishers/{p}/items/{id}/ratingSummary
        if (parts.Count < 5 || parts[0] != "v2" || parts[1] != "publishers" || parts[3] != "items")
            return Error(HttpStatusCode.NotFound, "Not found.");

        var publisherId = parts[2];
        var last = parts[4];
        var verb = last.Contains(':') ? last[(last.IndexOf(':') + 1)..] : parts.Count > 5 ? parts[5] : null;
        var itemId = last.Contains(':') ? last[..last.IndexOf(':')] : last;

        if (!Items.TryGetValue(itemId, out var item) || item.PublisherId != publisherId)
            return Error(HttpStatusCode.NotFound, $"Item {itemId} was not found.");

        if (item.Owner != identity)
            return Error(HttpStatusCode.Forbidden, "The caller does not have permission.");

        var body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var query = HttpUtility.ParseQueryString(request.RequestUri.Query);

        lock (_gate)
        {
            return verb switch
            {
                "fetchStatus" => Json(Status(itemId, item)),
                "upload" => Upload(itemId, item, body),
                "publish" => PublishCall(itemId, item, JsonNode.Parse(body is { Length: > 0 } ? Encoding.UTF8.GetString(body) : "{}")!.AsObject()),
                "setPublishedDeployPercentage" => DeployPercentage(item, JsonNode.Parse(Encoding.UTF8.GetString(body ?? []))!["deployPercentage"]!.GetValue<int>()),
                "cancelSubmission" => Cancel(item),
                "rollback" => Rollback(item, JsonNode.Parse(Encoding.UTF8.GetString(body ?? []))!.AsObject()),
                "fetchReviews" => Json(new JsonObject { ["reviews"] = new JsonArray([.. item.Reviews.Select(r => (JsonNode)r.DeepClone())]) }),
                "ratingSummary" => item.RatingSummary == null ? Error(HttpStatusCode.NotFound, "No ratings.") : Json(item.RatingSummary.DeepClone().AsObject()),
                "queryMetrics" => Metrics(item, query),
                _ => Error(HttpStatusCode.NotFound, $"Unknown method {verb}."),
            };
        }
    }

    private async Task<HttpResponseMessage> TokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var form = HttpUtility.ParseQueryString(await request.Content!.ReadAsStringAsync(cancellationToken));
        string identity;

        if (form["grant_type"] == "refresh_token")
        {
            if (form["refresh_token"] is not { } refresh || !Accounts.TryGetValue(refresh, out identity) || string.IsNullOrEmpty(form["client_secret"]))
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"Bad Request\"}", Encoding.UTF8, "application/json") };
        }
        else
        {
            var assertion = form["assertion"] ?? string.Empty;
            var payload = assertion.Split('.') is { Length: 3 } jwt ? JsonNode.Parse(Encoding.UTF8.GetString(Base64Url(jwt[1]))) : null;
            var email = payload?["iss"]?.GetValue<string>();

            if (email == null || !Accounts.TryGetValue(email, out identity) || payload["scope"]?.GetValue<string>() != "https://www.googleapis.com/auth/chromewebstore")
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"Invalid JWT Signature.\"}", Encoding.UTF8, "application/json") };
        }

        var token = $"cws-{Guid.NewGuid():N}";

        _tokens[token] = identity;

        return Json(new JsonObject { ["access_token"] = token, ["expires_in"] = 3599, ["token_type"] = "Bearer", ["scope"] = "https://www.googleapis.com/auth/chromewebstore" });
    }

    private static byte[] Base64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '='));
    }

    private static JsonObject RevisionJson(Revision revision) => revision == null ? null : new JsonObject
    {
        ["state"] = revision.State,
        ["distributionChannels"] = new JsonArray(new JsonObject { ["crxVersion"] = revision.Version, ["deployPercentage"] = revision.DeployPercentage ?? 100 }),
    };

    private static JsonObject Status(string itemId, Item item)
    {
        var status = new JsonObject { ["name"] = $"publishers/{item.PublisherId}/items/{itemId}", ["itemId"] = itemId, ["warned"] = item.Warned, ["takenDown"] = item.TakenDown };

        if (item.Published != null)
            status["publishedItemRevisionStatus"] = RevisionJson(item.Published);

        if (item.Submitted != null)
            status["submittedItemRevisionStatus"] = RevisionJson(item.Submitted);

        if (item.LastUploadState != null)
            status["lastAsyncUploadState"] = item.LastUploadState;

        // An upload in progress finishes on the next read.
        if (item.LastUploadState == "IN_PROGRESS")
            item.LastUploadState = "SUCCEEDED";

        return status;
    }

    private HttpResponseMessage Upload(string itemId, Item item, byte[] body)
    {
        ChromeManifest manifest;

        try
        {
            using var archive = new ZipArchive(new MemoryStream(body ?? []), ZipArchiveMode.Read);
            using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());

            manifest = ChromeManifest.Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is InvalidDataException or NullReferenceException)
        {
            item.LastUploadState = "FAILED";

            return Json(new JsonObject { ["itemId"] = itemId, ["uploadState"] = "FAILED" });
        }

        if (item.Published?.Version != null && ChromeVersions.Compare(manifest.Version, item.Published.Version) <= 0)
            return Error(HttpStatusCode.BadRequest, $"The version must be greater than {item.Published.Version}.");

        item.UploadedVersion = manifest.Version;

        if (UploadsInProgress)
        {
            item.LastUploadState = "IN_PROGRESS";

            return Json(new JsonObject { ["itemId"] = itemId, ["uploadState"] = "IN_PROGRESS" });
        }

        item.LastUploadState = "SUCCEEDED";

        return Json(new JsonObject { ["itemId"] = itemId, ["uploadState"] = "SUCCEEDED", ["crxVersion"] = manifest.Version });
    }

    private HttpResponseMessage PublishCall(string itemId, Item item, JsonObject body)
    {
        item.PublishBodies.Add(body);

        if (item.Submitted?.State == "PENDING_REVIEW")
            return Error(HttpStatusCode.BadRequest, "The item is already in review.");

        if (item.Submitted?.State == "STAGED" && item.UploadedVersion == item.Submitted.Version)
        {
            Publish(item, item.Submitted);
            item.UploadedVersion = null;

            return Json(new JsonObject { ["itemId"] = itemId, ["state"] = "PUBLISHED" });
        }

        if (item.UploadedVersion == null)
            return Error(HttpStatusCode.BadRequest, "There is no draft to publish.");

        var percentage = body["deployInfos"]?[0]?["deployPercentage"]?.GetValue<int>();
        var revision = new Revision { State = "PENDING_REVIEW", Version = item.UploadedVersion, DeployPercentage = percentage };

        if (body["skipReview"]?.GetValue<bool>() == true)
        {
            Publish(item, revision);
            item.UploadedVersion = null;

            return Json(new JsonObject { ["itemId"] = itemId, ["state"] = "PUBLISHED" });
        }

        item.Submitted = revision;

        if (body["publishType"]?.GetValue<string>() == "STAGED_PUBLISH")
            item.UploadedVersion = revision.Version;
        else
            item.UploadedVersion = null;

        return Json(new JsonObject { ["itemId"] = itemId, ["state"] = "PENDING_REVIEW", ["warningInfo"] = new JsonObject { ["warnings"] = new JsonArray(new JsonObject { ["reason"] = "PERMISSIONS", ["description"] = "Justify the tabs permission." }) } });
    }

    private static HttpResponseMessage DeployPercentage(Item item, int percentage)
    {
        if (item.Published == null)
            return Error(HttpStatusCode.BadRequest, "The item is not published.");

        if (percentage <= (item.Published.DeployPercentage ?? 0))
            return Error(HttpStatusCode.BadRequest, "The deploy percentage must be larger than the existing target percentage.");

        item.Published.DeployPercentage = percentage;

        return Json(new JsonObject());
    }

    private static HttpResponseMessage Cancel(Item item)
    {
        if (item.Submitted == null)
            return Error(HttpStatusCode.BadRequest, "There is no submission to cancel.");

        item.Submitted.State = "CANCELLED";

        return Json(new JsonObject());
    }

    private static HttpResponseMessage Rollback(Item item, JsonObject body)
    {
        var target = body["targetCrxVersion"]?.GetValue<string>() ?? item.PublishedVersions.ElementAtOrDefault(item.PublishedVersions.Count - 2);

        if (target == null || !item.PublishedVersions.Contains(target))
            return Error(HttpStatusCode.BadRequest, "No such earlier version.");

        Publish(item, new Revision { Version = body["newVersion"]!.GetValue<string>(), DeployPercentage = 100 });

        return Json(new JsonObject());
    }

    private static HttpResponseMessage Metrics(Item item, System.Collections.Specialized.NameValueCollection query)
    {
        var metrics = query.GetValues("metrics") ?? [];

        if (metrics.Contains("users-7d"))
        {
            var today = DateTime.UtcNow.Date;

            return Json(new JsonObject
            {
                ["metricRows"] = new JsonArray(new JsonObject
                {
                    ["date"] = new JsonObject { ["year"] = today.Year, ["month"] = today.Month, ["day"] = today.Day },
                    ["metricValues"] = new JsonArray(new JsonObject { ["metric"] = "users-7d", ["value"] = item.UsersLast7Days.ToString(System.Globalization.CultureInfo.InvariantCulture) }),
                }),
            });
        }

        return Json(new JsonObject { ["metricRows"] = new JsonArray([.. item.MetricRows.Select(r => (JsonNode)r.DeepClone())]) });
    }

    private static HttpResponseMessage Json(JsonObject body) => new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode status, string message)
        => new(status) { Content = new StringContent(new JsonObject { ["error"] = new JsonObject { ["code"] = (int)status, ["message"] = message, ["status"] = status.ToString() } }.ToJsonString(), Encoding.UTF8, "application/json") };
}
