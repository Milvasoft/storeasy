using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// <see cref="IChromeWebStoreClient"/> over the Chrome Web Store API v2 (named HTTP client <see cref="HttpClientName"/>).
/// An access token comes from the service account key (JWT bearer grant at the key's token endpoint) or from the OAuth
/// client's refresh token, and is kept until five minutes before it expires. Endpoints: <c>fetchStatus</c>,
/// <c>/upload/v2/...:upload</c>, <c>publish</c>, <c>setPublishedDeployPercentage</c>, <c>cancelSubmission</c>,
/// <c>rollback</c>, <c>fetchReviews</c>, <c>ratingSummary</c> and <c>queryMetrics</c>. Tests replace the handler with a fake
/// Chrome Web Store.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class ChromeWebStoreClient(IHttpClientFactory httpClientFactory, ChromeOptions options, TimeProvider timeProvider) : IChromeWebStoreClient
{
    /// <summary>
    /// Name of the HTTP client.
    /// </summary>
    public const string HttpClientName = "chrome";

    /// <summary>
    /// OAuth scope of the Chrome Web Store API (reads and writes).
    /// </summary>
    public const string Scope = "https://www.googleapis.com/auth/chromewebstore";

    private const int _maxErrorLength = 300;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ChromeOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expires)> _tokens = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async Task<StoreVerification> VerifyAsync(string secretJson, string itemId, CancellationToken cancellationToken)
    {
        var credentials = ChromeCredentialParser.Parse(secretJson, out var errorKey);

        if (credentials == null)
            return new StoreVerification(ConnectionStatus.Rejected, errorKey);

        try
        {
            await TokenAsync(credentials, cancellationToken, fresh: true);
        }
        catch (StoreCallException ex)
        {
            return new StoreVerification(ex.Transient ? ConnectionStatus.Unreachable : ConnectionStatus.Rejected, $"Google refused the credential: {ex.Message}");
        }

        var probe = string.IsNullOrWhiteSpace(itemId) ? credentials.ItemId : itemId.Trim();

        if (probe == null)
            return new StoreVerification(ConnectionStatus.Verified, null);

        try
        {
            await FetchStatusAsync(secretJson, probe, cancellationToken);
        }
        catch (StoreCallException ex)
        {
            var status = ex.Transient ? ConnectionStatus.Unreachable : ConnectionStatus.Rejected;

            return new StoreVerification(status, ex.StatusCode is 403 or 404
                ? $"The credential cannot read item {probe} of publisher {credentials.PublisherId}: {ex.Message}"
                : ex.Message);
        }

        return new StoreVerification(ConnectionStatus.Verified, null);
    }

    /// <inheritdoc/>
    public async Task<ChromeItemStatusRead> FetchStatusAsync(string secretJson, string itemId, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);
        var root = await SendJsonAsync(token, HttpMethod.Get, $"/v2/{ItemName(credentials, itemId)}:fetchStatus", null, cancellationToken);

        return ParseStatus(root, itemId);
    }

    /// <summary>
    /// Maps a <c>FetchItemStatusResponse</c>.
    /// </summary>
    /// <param name="root"></param>
    /// <param name="itemId"></param>
    /// <returns></returns>
    public static ChromeItemStatusRead ParseStatus(JsonNode root, string itemId)
        => new(Text(root, "itemId") ?? itemId,
               Revision(root?["publishedItemRevisionStatus"]),
               Revision(root?["submittedItemRevisionStatus"]),
               UploadStateOf(Text(root, "lastAsyncUploadState")),
               root?["warned"]?.GetValue<bool>() == true,
               root?["takenDown"]?.GetValue<bool>() == true);

    /// <inheritdoc/>
    public async Task<ChromeUploadRead> UploadAsync(string secretJson, string itemId, Stream package, long length, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, Url($"/upload/v2/{ItemName(credentials, itemId)}:upload"));
        var content = new StreamContent(package);

        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Headers.ContentLength = length;
        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendAsync(request, cancellationToken);
        var root = await ReadJsonAsync(response, cancellationToken);

        return new ChromeUploadRead(UploadStateOf(Text(root, "uploadState")), Text(root, "crxVersion"));
    }

    /// <inheritdoc/>
    public async Task<ChromePublishRead> PublishAsync(string secretJson, string itemId, ChromePublishRequest request, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);
        var body = new JsonObject { ["publishType"] = request.Staged ? "STAGED_PUBLISH" : "DEFAULT_PUBLISH" };

        if (request.DeployPercentage is int percentage)
            body["deployInfos"] = new JsonArray(new JsonObject { ["deployPercentage"] = percentage });

        if (request.SkipReview)
            body["skipReview"] = true;

        var root = await SendJsonAsync(token, HttpMethod.Post, $"/v2/{ItemName(credentials, itemId)}:publish", body, cancellationToken);
        var warnings = (root?["warningInfo"]?["warnings"] as JsonArray ?? []).Select(w => Text(w, "description") ?? Text(w, "reason")).Where(w => w != null).ToList();

        return new ChromePublishRead(StateOf(Text(root, "state")), warnings);
    }

    /// <inheritdoc/>
    public async Task SetDeployPercentageAsync(string secretJson, string itemId, int percentage, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);

        await SendJsonAsync(token, HttpMethod.Post, $"/v2/{ItemName(credentials, itemId)}:setPublishedDeployPercentage", new JsonObject { ["deployPercentage"] = percentage }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CancelSubmissionAsync(string secretJson, string itemId, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);

        await SendJsonAsync(token, HttpMethod.Post, $"/v2/{ItemName(credentials, itemId)}:cancelSubmission", new JsonObject(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(string secretJson, string itemId, ChromeRollbackRequest request, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);
        var body = new JsonObject { ["newVersion"] = request.NewVersion, ["rollbackReason"] = request.Reason };

        if (request.TargetVersion != null)
            body["targetCrxVersion"] = request.TargetVersion;

        await SendJsonAsync(token, HttpMethod.Post, $"/v2/{ItemName(credentials, itemId)}:rollback", body, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreReviewRecord>> FetchReviewsAsync(string secretJson, string itemId, int maxPages, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);
        var reviews = new List<StoreReviewRecord>();
        string pageToken = null;

        for (var page = 0; page < Math.Max(1, maxPages); page++)
        {
            var path = $"/v2/{ItemName(credentials, itemId)}:fetchReviews?pageSize=200&orderBy={Uri.EscapeDataString("create_time desc")}"
                       + (pageToken == null ? string.Empty : $"&pageToken={Uri.EscapeDataString(pageToken)}");
            var root = await SendJsonAsync(token, HttpMethod.Get, path, null, cancellationToken);

            reviews.AddRange((root?["reviews"] as JsonArray ?? []).Select(ParseReview).Where(r => r != null));

            pageToken = Text(root, "nextPageToken");

            if (string.IsNullOrEmpty(pageToken))
                break;
        }

        return reviews;
    }

    /// <summary>
    /// Maps a <c>Review</c>; null without a name.
    /// </summary>
    /// <param name="review"></param>
    /// <returns></returns>
    public static StoreReviewRecord ParseReview(JsonNode review)
    {
        var name = Text(review, "name");

        if (string.IsNullOrEmpty(name))
            return null;

        var id = name[(name.LastIndexOf('/') + 1)..];
        var date = DateTimeOffset.TryParse(Text(review, "createTime"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created) ? created.UtcDateTime : DateTime.UnixEpoch;
        var rating = review?["rating"] is JsonValue value && value.TryGetValue<int>(out var stars) ? stars : 0;

        return new StoreReviewRecord(id, Text(review?["authorInfo"], "displayName"), rating, null, Text(review, "comment"), null, null, null, null, date, null, null, null);
    }

    /// <inheritdoc/>
    public async Task<ChromeRatingSummaryRead> GetRatingSummaryAsync(string secretJson, string itemId, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);
        var root = await SendJsonAsync(token, HttpMethod.Get, $"/v2/{ItemName(credentials, itemId)}/ratingSummary", null, cancellationToken);
        var count = Long(root, "ratingCount") ?? 0;
        double? rating = root?["rating"] is JsonValue value && value.TryGetValue<double>(out var average) && count > 0 ? average : null;

        return new ChromeRatingSummaryRead(rating, count, Long(root, "nonEmptyReviewCount") ?? 0);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChromeMetricRead>> QueryMetricsAsync(string secretJson, string itemId, IReadOnlyList<string> metrics, string dimension, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (credentials, token) = await OpenAsync(secretJson, cancellationToken);
        var rows = new List<ChromeMetricRead>();
        var query = new StringBuilder();

        foreach (var metric in metrics)
            query.Append("&metrics=").Append(Uri.EscapeDataString(metric));

        if (dimension != null)
            query.Append("&dimensions=").Append(Uri.EscapeDataString(dimension));

        query.Append(CultureInfo.InvariantCulture, $"&timeSpec.firstDate.year={from.Year}&timeSpec.firstDate.month={from.Month}&timeSpec.firstDate.day={from.Day}");
        query.Append(CultureInfo.InvariantCulture, $"&timeSpec.lastDate.year={to.Year}&timeSpec.lastDate.month={to.Month}&timeSpec.lastDate.day={to.Day}");

        string pageToken = null;

        // A bounded number of pages: 90 days of 250 countries fit in well under 50 pages of 1000 rows.
        for (var page = 0; page < 50; page++)
        {
            var path = $"/v2/{ItemName(credentials, itemId)}:queryMetrics?pageSize=1000{query}" + (pageToken == null ? string.Empty : $"&pageToken={Uri.EscapeDataString(pageToken)}");
            var root = await SendJsonAsync(token, HttpMethod.Get, path, null, cancellationToken);

            rows.AddRange((root?["metricRows"] as JsonArray ?? []).Select(ParseMetricRow).Where(r => r != null));

            pageToken = Text(root, "nextPageToken");

            if (string.IsNullOrEmpty(pageToken))
                break;
        }

        return rows;
    }

    /// <summary>
    /// Maps a <c>MetricRow</c>; null without a full date.
    /// </summary>
    /// <param name="row"></param>
    /// <returns></returns>
    public static ChromeMetricRead ParseMetricRow(JsonNode row)
    {
        var date = row?["date"];
        var year = Int(date, "year");
        var month = Int(date, "month");
        var day = Int(date, "day");

        if (year is not > 0 || month is not (>= 1 and <= 12) || day is not (>= 1 and <= 31) || day > DateTime.DaysInMonth(year.Value, month.Value))
            return null;

        var values = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var value in row["metricValues"] as JsonArray ?? [])
        {
            var metric = Text(value, "metric");

            if (metric != null && Long(value, "value") is long number)
                values[metric] = number;
        }

        var dimension = (row["dimensionValues"] as JsonArray)?.FirstOrDefault() is JsonValue text && text.TryGetValue<string>(out var dimensionValue) ? dimensionValue : null;

        return new ChromeMetricRead(new DateOnly(year.Value, month.Value, day.Value), dimension, values);
    }

    /// <summary>
    /// Maps an <c>ItemState</c> value.
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ChromeItemState StateOf(string state) => state switch
    {
        "PENDING_REVIEW" => ChromeItemState.PendingReview,
        "STAGED" => ChromeItemState.Staged,
        "PUBLISHED" => ChromeItemState.Published,
        "PUBLISHED_TO_TESTERS" => ChromeItemState.PublishedToTesters,
        "REJECTED" => ChromeItemState.Rejected,
        "CANCELLED" => ChromeItemState.Cancelled,
        _ => ChromeItemState.None,
    };

    /// <summary>
    /// Maps an <c>UploadState</c> value.
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ChromeUploadState UploadStateOf(string state) => state switch
    {
        "SUCCEEDED" => ChromeUploadState.Succeeded,
        "IN_PROGRESS" or "UPLOAD_IN_PROGRESS" => ChromeUploadState.InProgress,
        "FAILED" => ChromeUploadState.Failed,
        "NOT_FOUND" => ChromeUploadState.Missing,
        _ => ChromeUploadState.None,
    };

    private static ChromeRevisionRead Revision(JsonNode revision)
    {
        if (revision is not JsonObject)
            return null;

        var channel = (revision["distributionChannels"] as JsonArray ?? []).FirstOrDefault(c => Text(c, "crxVersion") != null) ?? (revision["distributionChannels"] as JsonArray)?.FirstOrDefault();

        return new ChromeRevisionRead(StateOf(Text(revision, "state")), Text(channel, "crxVersion"), Int(channel, "deployPercentage"));
    }

    #region Token and HTTP

    private async Task<(ChromeCredentials Credentials, string Token)> OpenAsync(string secretJson, CancellationToken cancellationToken)
    {
        var credentials = ChromeCredentialParser.Parse(secretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

        return (credentials, await TokenAsync(credentials, cancellationToken));
    }

    private async Task<string> TokenAsync(ChromeCredentials credentials, CancellationToken cancellationToken, bool fresh = false)
    {
        var key = ChromeCredentialParser.CacheKey(credentials);
        var now = _timeProvider.GetUtcNow();

        if (!fresh && _tokens.TryGetValue(key, out var cached) && cached.Expires > now.AddMinutes(5))
            return cached.Token;

        string tokenUrl;
        Dictionary<string, string> form;

        if (credentials.IsServiceAccount)
        {
            var account = credentials.ServiceAccount ?? throw new StoreCallException(MessageKey.ChromeServiceAccountInvalid, null, false);

            tokenUrl = account.TokenUri;
            form = new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = StoreJwt.CreateGoogleAssertion(account, Scope, now),
            };
        }
        else
        {
            tokenUrl = _options.TokenUrl;
            form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
                ["refresh_token"] = credentials.RefreshToken,
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) { Content = new FormUrlEncodedContent(form) };
        using var response = await SendAsync(request, cancellationToken);
        var root = await ReadJsonAsync(response, cancellationToken);
        var token = Text(root, "access_token") ?? throw new StoreCallException("Google returned no access token.", null, false);

        // A refresh token granted without the Chrome Web Store scope gives a token the API refuses; say so up front.
        if (Text(root, "scope") is { } scope && !scope.Split(' ').Contains(Scope))
            throw new StoreCallException($"The token does not have the {Scope} scope; authorize that scope when creating the refresh token.", 403, false);

        var expiresIn = Int(root, "expires_in") ?? 3600;

        _tokens[key] = (token, now.AddSeconds(expiresIn));

        return token;
    }

    private string Url(string path) => _options.ApiBaseUrl.TrimEnd('/') + path;

    private static string ItemName(ChromeCredentials credentials, string itemId) => $"publishers/{Uri.EscapeDataString(credentials.PublisherId)}/items/{Uri.EscapeDataString(itemId)}";

    private async Task<JsonNode> SendJsonAsync(string token, HttpMethod method, string path, JsonNode body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, Url(path));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body != null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, cancellationToken);

        return await ReadJsonAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException(SecretRedactor.RedactAndTrim($"Chrome Web Store unreachable: {ex.Message}", _maxErrorLength), null, true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StoreCallException("The Chrome Web Store did not answer in time.", null, true);
        }

        if (response.IsSuccessStatusCode)
            return response;

        using (response)
        {
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var transient = status >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;

            throw new StoreCallException(SecretRedactor.RedactAndTrim($"{status}: {DescribeError(text)}", _maxErrorLength), status, transient);
        }
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text) ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException)
        {
            throw new StoreCallException("The Chrome Web Store returned an answer that is not JSON.", (int)response.StatusCode, false);
        }
    }

    /// <summary>
    /// The message of a Google API error body (<c>{"error": {"message": ...}}</c> or an OAuth error), or the body itself.
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public static string DescribeError(string body)
    {
        try
        {
            var root = JsonNode.Parse(body ?? string.Empty);

            if (root?["error"] is JsonObject error)
                return Text(error, "message") ?? Text(error, "status") ?? body;

            if (Text(root, "error") is { } oauth)
                return Text(root, "error_description") is { } description ? $"{oauth}: {description}" : oauth;
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(body) ? "no details" : body;
    }

    private static string Text(JsonNode node, string name)
        => node is JsonObject obj && obj.TryGetPropertyValue(name, out var value) && value is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    private static int? Int(JsonNode node, string name)
    {
        if (node is not JsonObject obj || !obj.TryGetPropertyValue(name, out var value) || value is not JsonValue v)
            return null;

        if (v.TryGetValue<int>(out var number))
            return number;

        return v.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static long? Long(JsonNode node, string name)
    {
        if (node is not JsonObject obj || !obj.TryGetPropertyValue(name, out var value) || value is not JsonValue v)
            return null;

        if (v.TryGetValue<long>(out var number))
            return number;

        return v.TryGetValue<string>(out var text) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    #endregion
}
