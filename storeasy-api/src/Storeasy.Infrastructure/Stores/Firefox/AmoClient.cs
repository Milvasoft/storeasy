using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Firefox;

/// <summary>
/// <see cref="IAmoClient"/> over the addons.mozilla.org API v5 (named HTTP client <see cref="HttpClientName"/>). Every
/// request carries <c>Authorization: JWT &lt;token&gt;</c>, a fresh HS256 token from the connection's issuer and secret.
/// Translated fields are read without <c>lang</c>, so every locale comes back. Tests replace the handler with a fake AMO.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class AmoClient(IHttpClientFactory httpClientFactory, FirefoxOptions options, TimeProvider timeProvider) : IAmoClient
{
    /// <summary>
    /// Name of the HTTP client.
    /// </summary>
    public const string HttpClientName = "amo";

    private const int _maxErrorLength = 300;
    private const int _maxAddonPages = 20;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly FirefoxOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<AmoProfileRead> GetProfileAsync(string secretJson, CancellationToken cancellationToken)
    {
        var root = await SendJsonAsync(secretJson, HttpMethod.Get, "/accounts/profile/", null, cancellationToken);

        return new AmoProfileRead(Long(root, "id") ?? 0, Text(root, "username"), Text(root, "name"));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AmoAddonRead>> ListAddonsAsync(string secretJson, CancellationToken cancellationToken)
    {
        var addons = new List<AmoAddonRead>();

        await foreach (var item in PagesAsync(secretJson, "/addons/addon/?page_size=50", _maxAddonPages, cancellationToken))
            addons.Add(ParseAddon(item));

        return addons;
    }

    /// <inheritdoc/>
    public async Task<AmoAddonRead> GetAddonAsync(string secretJson, string addon, CancellationToken cancellationToken)
        => ParseAddon(await SendJsonAsync(secretJson, HttpMethod.Get, $"/addons/addon/{Uri.EscapeDataString(addon)}/", null, cancellationToken));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AmoVersionRead>> ListVersionsAsync(string secretJson, string addon, int maxPages, CancellationToken cancellationToken)
    {
        var versions = new List<AmoVersionRead>();

        await foreach (var item in PagesAsync(secretJson, $"/addons/addon/{Uri.EscapeDataString(addon)}/versions/?filter=all_with_unlisted&page_size=50", Math.Max(1, maxPages), cancellationToken))
            versions.Add(ParseVersion(item));

        return versions;
    }

    /// <inheritdoc/>
    public async Task<AmoAddonRead> UpdateAddonAsync(string secretJson, string addon, JsonObject patch, CancellationToken cancellationToken)
        => ParseAddon(await SendJsonAsync(secretJson, HttpMethod.Patch, $"/addons/addon/{Uri.EscapeDataString(addon)}/", patch, cancellationToken));

    /// <inheritdoc/>
    public async Task UploadIconAsync(string secretJson, string addon, AmoFile file, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();

        form.Add(FileContent(file), "icon", file.FileName);
        await SendFormAsync(secretJson, HttpMethod.Patch, $"/addons/addon/{Uri.EscapeDataString(addon)}/", form, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AmoPreviewRead> AddPreviewAsync(string secretJson, string addon, AmoFile file, int? position, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();

        form.Add(FileContent(file), "image", file.FileName);

        if (position is int value)
            form.Add(new StringContent(value.ToString(CultureInfo.InvariantCulture)), "position");

        return ParsePreview(await SendFormAsync(secretJson, HttpMethod.Post, $"/addons/addon/{Uri.EscapeDataString(addon)}/previews/", form, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task DeletePreviewAsync(string secretJson, string addon, long previewId, CancellationToken cancellationToken)
        => await SendJsonAsync(secretJson, HttpMethod.Delete, $"/addons/addon/{Uri.EscapeDataString(addon)}/previews/{previewId.ToString(CultureInfo.InvariantCulture)}/", null, cancellationToken);

    /// <inheritdoc/>
    public async Task<AmoUploadRead> UploadAsync(string secretJson, AmoFile file, FirefoxVersionChannel channel, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();

        form.Add(FileContent(file), "upload", file.FileName);
        form.Add(new StringContent(ChannelName(channel)), "channel");

        return ParseUpload(await SendFormAsync(secretJson, HttpMethod.Post, "/addons/upload/", form, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<AmoUploadRead> GetUploadAsync(string secretJson, string uuid, CancellationToken cancellationToken)
        => ParseUpload(await SendJsonAsync(secretJson, HttpMethod.Get, $"/addons/upload/{Uri.EscapeDataString(uuid)}/", null, cancellationToken));

    /// <inheritdoc/>
    public async Task<AmoVersionRead> CreateVersionAsync(string secretJson, string addon, AmoVersionCreate request, AmoFile source, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["upload"] = request.UploadUuid };

        if (request.ReleaseNotes?.Count > 0)
            body["release_notes"] = Translations(request.ReleaseNotes);

        if (!string.IsNullOrWhiteSpace(request.ApprovalNotes))
            body["approval_notes"] = request.ApprovalNotes;

        if (!string.IsNullOrWhiteSpace(request.License))
            body["license"] = request.License;

        if (request.Compatibility?.Count > 0)
            body["compatibility"] = new JsonArray([.. request.Compatibility.Select(a => (JsonNode)JsonValue.Create(a))]);

        var created = ParseVersion(await SendJsonAsync(secretJson, HttpMethod.Post, $"/addons/addon/{Uri.EscapeDataString(addon)}/versions/", body, cancellationToken));

        if (source == null)
            return created;

        // Source code goes in only as multipart form data.
        using var form = new MultipartFormDataContent();

        form.Add(FileContent(source), "source", source.FileName);

        var updated = ParseVersion(await SendFormAsync(secretJson, HttpMethod.Patch, $"/addons/addon/{Uri.EscapeDataString(addon)}/versions/{created.Id.ToString(CultureInfo.InvariantCulture)}/", form, cancellationToken));

        return updated.Id == 0 ? created : updated;
    }

    /// <inheritdoc/>
    public async Task<AmoVersionRead> UpdateVersionAsync(string secretJson, string addon, long versionId, IReadOnlyDictionary<string, string> releaseNotes, string approvalNotes, CancellationToken cancellationToken)
    {
        var body = new JsonObject();

        if (releaseNotes?.Count > 0)
            body["release_notes"] = Translations(releaseNotes);

        if (approvalNotes != null)
            body["approval_notes"] = approvalNotes;

        return ParseVersion(await SendJsonAsync(secretJson, HttpMethod.Patch, $"/addons/addon/{Uri.EscapeDataString(addon)}/versions/{versionId.ToString(CultureInfo.InvariantCulture)}/", body, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreReviewRecord>> ListRatingsAsync(string secretJson, string addon, int maxPages, CancellationToken cancellationToken)
    {
        var ratings = new List<StoreReviewRecord>();

        await foreach (var item in PagesAsync(secretJson, $"/ratings/rating/?addon={Uri.EscapeDataString(addon)}&page_size=50", Math.Max(1, maxPages), cancellationToken))
        {
            if (ParseRating(item) is { } rating)
                ratings.Add(rating);
        }

        return ratings;
    }

    /// <inheritdoc/>
    public async Task<StoreReplyRecord> ReplyAsync(string secretJson, string ratingId, string body, CancellationToken cancellationToken)
    {
        var root = await SendJsonAsync(secretJson, HttpMethod.Post, $"/ratings/rating/{Uri.EscapeDataString(ratingId)}/reply/", new JsonObject { ["body"] = body }, cancellationToken);

        return new StoreReplyRecord(Long(root, "id")?.ToString(CultureInfo.InvariantCulture), Date(root, "created") ?? _timeProvider.GetUtcNow().UtcDateTime);
    }

    #region Parsing

    /// <summary>
    /// Maps an add-on detail.
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    public static AmoAddonRead ParseAddon(JsonNode root)
    {
        var addon = new AmoAddonRead
        {
            Id = Long(root, "id") ?? 0,
            Guid = Text(root, "guid"),
            Slug = Text(root, "slug"),
            Type = Text(root, "type"),
            Status = StatusOf(Text(root, "status")),
            IsDisabled = Bool(root, "is_disabled"),
            DefaultLocale = Text(root, "default_locale"),
            Name = Translated(root?["name"]),
            Summary = Translated(root?["summary"]),
            Description = Translated(root?["description"]),
            Homepage = Translated(Link(root?["homepage"])),
            SupportUrl = Translated(Link(root?["support_url"])),
            SupportEmail = Translated(root?["support_email"]),
            IconUrl = Text(root, "icon_url"),
            Url = Text(root, "url"),
            EditUrl = Text(root, "edit_url"),
            AverageDailyUsers = Long(root, "average_daily_users"),
            WeeklyDownloads = Long(root, "weekly_downloads"),
        };

        // Flattened list since 2023; the older shape is an object of lists keyed by application.
        addon.Categories = root?["categories"] switch
        {
            JsonArray list => [.. list.Select(c => c is JsonValue v && v.TryGetValue<string>(out var slug) ? slug : null).Where(c => c != null).Distinct(StringComparer.Ordinal)],
            JsonObject byApp => [.. byApp.Where(a => a.Key == "firefox").SelectMany(a => a.Value as JsonArray ?? []).Select(c => c is JsonValue v && v.TryGetValue<string>(out var slug) ? slug : null).Where(c => c != null).Distinct(StringComparer.Ordinal)],
            _ => [],
        };

        addon.Previews = [.. (root?["previews"] as JsonArray ?? []).Select(ParsePreview).Where(p => p.Id > 0)];

        if (root?["current_version"] is JsonObject current)
            addon.CurrentVersion = ParseVersion(current);

        var ratings = root?["ratings"];

        addon.RatingCount = Long(ratings, "count") ?? 0;
        addon.Rating = addon.RatingCount > 0 ? Double(ratings, "average") : null;

        return addon;
    }

    /// <summary>
    /// Maps a version.
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    public static AmoVersionRead ParseVersion(JsonNode root)
    {
        var file = root?["file"];
        var license = root?["license"];

        return new AmoVersionRead
        {
            Id = Long(root, "id") ?? 0,
            Version = Text(root, "version"),
            Channel = Text(root, "channel") switch
            {
                "listed" => FirefoxVersionChannel.Listed,
                "unlisted" => FirefoxVersionChannel.Unlisted,
                _ => FirefoxVersionChannel.None,
            },
            FileStatus = Text(file, "status") switch
            {
                "public" => FirefoxFileStatus.Public,
                "unreviewed" or "awaiting_review" => FirefoxFileStatus.Unreviewed,
                "disabled" => FirefoxFileStatus.Disabled,
                _ => FirefoxFileStatus.None,
            },
            IsDisabled = Bool(root, "is_disabled"),
            ReleaseNotes = Translated(root?["release_notes"]),
            License = Text(license, "slug") ?? Translated(license?["name"]).Values.FirstOrDefault(),
            HasSource = !string.IsNullOrEmpty(Text(root, "source")),
            Created = Date(file, "created"),
            FileUrl = Text(file, "url"),
            FileSize = Long(file, "size"),
            Permissions = [.. Strings(file, "permissions").Concat(Strings(file, "host_permissions"))],
            DataCollectionPermissions = [.. Strings(file, "data_collection_permissions")],
            MinFirefox = Text(root?["compatibility"]?["firefox"], "min"),
        };
    }

    /// <summary>
    /// Maps an upload and its validation.
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    public static AmoUploadRead ParseUpload(JsonNode root)
    {
        var validation = root?["validation"];
        var messages = (validation?["messages"] as JsonArray ?? [])
                       .Where(m => Text(m, "type") is "error" or "warning")
                       .OrderBy(m => Text(m, "type") == "error" ? 0 : 1)
                       .Select(m => $"{Text(m, "type")}: {StripTags(Text(m, "message"))}")
                       .Take(10)
                       .ToList();

        return new AmoUploadRead(Text(root, "uuid"),
                                 Bool(root, "processed"),
                                 Bool(root, "valid"),
                                 Bool(root, "submitted"),
                                 Text(root, "version"),
                                 (int)(Long(validation, "errors") ?? 0),
                                 (int)(Long(validation, "warnings") ?? 0),
                                 messages);
    }

    /// <summary>
    /// Maps a rating; null for a reply or a rating without an id.
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    public static StoreReviewRecord ParseRating(JsonNode root)
    {
        var id = Long(root, "id");

        if (id is not > 0 || Bool(root, "is_developer_reply"))
            return null;

        var reply = root?["reply"] as JsonObject;
        var score = (int)(Long(root, "score") ?? Long(root, "rating") ?? 0);

        return new StoreReviewRecord(id.Value.ToString(CultureInfo.InvariantCulture),
                                     Text(root?["user"], "name") ?? Text(root?["user"], "username"),
                                     score,
                                     null,
                                     Text(root, "body"),
                                     null,
                                     null,
                                     Text(root?["version"], "version"),
                                     null,
                                     Date(root, "created") ?? DateTime.UnixEpoch,
                                     reply == null ? null : Long(reply, "id")?.ToString(CultureInfo.InvariantCulture),
                                     reply == null ? null : Text(reply, "body"),
                                     reply == null ? null : Date(reply, "created"));
    }

    /// <summary>
    /// Maps an add-on status value.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static FirefoxAddonStatus StatusOf(string status) => status switch
    {
        "public" or "approved" => FirefoxAddonStatus.Public,
        "nominated" or "awaiting_review" => FirefoxAddonStatus.Nominated,
        "incomplete" or "null" => FirefoxAddonStatus.Incomplete,
        "disabled" => FirefoxAddonStatus.Disabled,
        "rejected" => FirefoxAddonStatus.Rejected,
        "deleted" => FirefoxAddonStatus.Deleted,
        _ => FirefoxAddonStatus.None,
    };

    /// <summary>
    /// API name of a channel.
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public static string ChannelName(FirefoxVersionChannel channel) => channel == FirefoxVersionChannel.Unlisted ? "unlisted" : "listed";

    private static AmoPreviewRead ParsePreview(JsonNode root)
    {
        var size = root?["image_size"] as JsonArray;
        int? width = size?.Count == 2 && size[0] is JsonValue w && w.TryGetValue<int>(out var x) ? x : null;
        int? height = size?.Count == 2 && size[1] is JsonValue h && h.TryGetValue<int>(out var y) ? y : null;
        var caption = Translated(root?["caption"]);

        return new AmoPreviewRead(Long(root, "id") ?? 0, Text(root, "image_url"), Text(root, "thumbnail_url"), (int)(Long(root, "position") ?? 0), caption.Values.FirstOrDefault(), width, height);
    }

    /// <summary>
    /// A translated field: an object of locale to text (keys starting with <c>_</c> are skipped), or a plain text kept
    /// under <c>_</c>.
    /// </summary>
    private static Dictionary<string, string> Translated(JsonNode node)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (node is JsonObject obj)
        {
            foreach (var (locale, value) in obj)
            {
                if (!locale.StartsWith('_') && value is JsonValue v && v.TryGetValue<string>(out var text))
                    values[locale] = text;
            }
        }
        else if (node is JsonValue plain && plain.TryGetValue<string>(out var single))
        {
            values["_"] = single;
        }

        return values;
    }

    /// <summary>
    /// <c>homepage</c> and <c>support_url</c> are <c>{"url": {...}, "outgoing": {...}}</c> in v5; older answers give the
    /// translated object directly.
    /// </summary>
    private static JsonNode Link(JsonNode node) => node is JsonObject obj && obj["url"] is JsonNode url ? url : node;

    private static JsonObject Translations(IReadOnlyDictionary<string, string> texts)
    {
        var obj = new JsonObject();

        foreach (var (locale, text) in texts)
            obj[locale] = text == null ? null : JsonValue.Create(text);

        return obj;
    }

    private static string StripTags(string text)
        => text == null ? null : System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));

    #endregion

    #region HTTP

    private string Url(string path) => _options.ApiBaseUrl.TrimEnd('/') + path;

    private string Token(string secretJson)
    {
        var credentials = FirefoxCredentialParser.Parse(secretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

        return FirefoxCredentialParser.CreateToken(credentials, _timeProvider.GetUtcNow(), _options.TokenLifetimeSeconds);
    }

    private async IAsyncEnumerable<JsonNode> PagesAsync(string secretJson, string path, int maxPages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var url = Url(path);
        var origin = new Uri(url).GetLeftPart(UriPartial.Authority);

        for (var page = 0; page < maxPages && url != null; page++)
        {
            var root = await SendAsync(secretJson, HttpMethod.Get, url, null, cancellationToken);

            foreach (var item in root?["results"] as JsonArray ?? [])
                yield return item;

            var next = Text(root, "next");

            // Only follow pages of the same server.
            url = next != null && next.StartsWith(origin + "/", StringComparison.OrdinalIgnoreCase) ? next : null;
        }
    }

    private Task<JsonNode> SendJsonAsync(string secretJson, HttpMethod method, string path, JsonNode body, CancellationToken cancellationToken)
        => SendAsync(secretJson, method, Url(path), body == null ? null : new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), cancellationToken);

    private Task<JsonNode> SendFormAsync(string secretJson, HttpMethod method, string path, MultipartFormDataContent form, CancellationToken cancellationToken)
        => SendAsync(secretJson, method, Url(path), form, cancellationToken, disposeContent: false);

    private async Task<JsonNode> SendAsync(string secretJson, HttpMethod method, string url, HttpContent content, CancellationToken cancellationToken, bool disposeContent = true)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        request.Headers.Authorization = new AuthenticationHeaderValue("JWT", Token(secretJson));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;

        try
        {
            response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException(SecretRedactor.RedactAndTrim($"addons.mozilla.org unreachable: {ex.Message}", _maxErrorLength), null, true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StoreCallException("addons.mozilla.org did not answer in time.", null, true);
        }
        finally
        {
            if (!disposeContent)
                request.Content = null;
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var transient = status >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;

                throw new StoreCallException(SecretRedactor.RedactAndTrim($"{status}: {DescribeError(text)}", _maxErrorLength), status, transient);
            }

            try
            {
                return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text) ?? new JsonObject();
            }
            catch (System.Text.Json.JsonException)
            {
                throw new StoreCallException("addons.mozilla.org returned an answer that is not JSON.", (int)response.StatusCode, false);
            }
        }
    }

    private static StreamContent FileContent(AmoFile file)
    {
        var content = new StreamContent(file.Content);

        content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);

        if (file.Length > 0)
            content.Headers.ContentLength = file.Length;

        return content;
    }

    /// <summary>
    /// The message of an AMO error body: <c>{"detail": ...}</c>, field errors (<c>{"field": ["..."]}</c>) or
    /// <c>non_field_errors</c>; the body itself otherwise.
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public static string DescribeError(string body)
    {
        try
        {
            var root = JsonNode.Parse(body ?? string.Empty);

            if (Text(root, "detail") is { } detail)
                return detail;

            if (root is JsonObject obj && obj.Count > 0)
            {
                return string.Join("; ", obj.Select(f => f.Value switch
                {
                    JsonArray list => $"{f.Key}: {string.Join(" ", list.Select(v => v is JsonValue value && value.TryGetValue<string>(out var t) ? t : v?.ToJsonString()))}",
                    JsonValue value when value.TryGetValue<string>(out var t) => $"{f.Key}: {t}",
                    _ => $"{f.Key}: {f.Value?.ToJsonString()}",
                }));
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(body) ? "no details" : body;
    }

    private static string Text(JsonNode node, string name)
        => node is JsonObject obj && obj.TryGetPropertyValue(name, out var value) && value is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    private static bool Bool(JsonNode node, string name)
        => node is JsonObject obj && obj.TryGetPropertyValue(name, out var value) && value is JsonValue v && v.TryGetValue<bool>(out var flag) && flag;

    private static long? Long(JsonNode node, string name)
    {
        if (node is not JsonObject obj || !obj.TryGetPropertyValue(name, out var value) || value is not JsonValue v)
            return null;

        if (v.TryGetValue<long>(out var number))
            return number;

        if (v.TryGetValue<double>(out var real))
            return (long)real;

        return v.TryGetValue<string>(out var text) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static double? Double(JsonNode node, string name)
    {
        if (node is not JsonObject obj || !obj.TryGetPropertyValue(name, out var value) || value is not JsonValue v)
            return null;

        if (v.TryGetValue<double>(out var number))
            return number;

        return v.TryGetValue<string>(out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static DateTime? Date(JsonNode node, string name)
        => DateTimeOffset.TryParse(Text(node, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date.UtcDateTime : null;

    private static IEnumerable<string> Strings(JsonNode node, string name)
        => (node?[name] as JsonArray ?? []).Select(v => v is JsonValue value && value.TryGetValue<string>(out var text) ? text : null).Where(t => t != null);

    #endregion
}
