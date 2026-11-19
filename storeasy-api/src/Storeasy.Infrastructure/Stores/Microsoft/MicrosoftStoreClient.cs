using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// <see cref="IMicrosoftStoreClient"/> over the named HTTP client <see cref="HttpClientName"/> (tests replace its handler
/// with a fake Microsoft). Tokens come from the Entra v2.0 client credentials endpoint, one per API scope, cached per
/// connection in <see cref="MicrosoftTokenCache"/>. Every call is retried on 429 (honouring Retry-After), 5xx and network
/// failures with exponential backoff; a 401 drops the cached token and tries once more. Log lines carry the method, the
/// path without its query (SAS signatures live there) and the status; error texts are redacted.
/// </summary>
/// <param name="HttpClientFactory"></param>
/// <param name="TokenCache"></param>
/// <param name="Options"></param>
/// <param name="TimeProvider"></param>
/// <param name="Logger"></param>
public sealed partial class MicrosoftStoreClient(IHttpClientFactory HttpClientFactory,
                                                 MicrosoftTokenCache TokenCache,
                                                 MicrosoftStoreOptions Options,
                                                 TimeProvider TimeProvider,
                                                 IMilvaLogger Logger) : IMicrosoftStoreClient
{
    /// <summary>
    /// Name of the HTTP client used for Microsoft calls, blob uploads and downloads.
    /// </summary>
    public const string HttpClientName = "microsoft-store";

    private const int _maxErrorLength = 300;

    private readonly IHttpClientFactory _httpClientFactory = HttpClientFactory;
    private readonly MicrosoftTokenCache _tokenCache = TokenCache;
    private readonly MicrosoftStoreOptions _options = Options;
    private readonly TimeProvider _timeProvider = TimeProvider;
    private readonly IMilvaLogger _logger = Logger;

    private enum Api
    {
        DevCenter = 1,
        StoreApi = 2,
    }

    #region Verification and MSIX apps

    /// <inheritdoc/>
    public async Task<StoreVerification> VerifyAsync(string secretJson, CancellationToken cancellationToken)
    {
        var credentials = MicrosoftCredentialParser.Parse(secretJson, out var errorKey);

        if (credentials == null)
            return new StoreVerification(ConnectionStatus.Rejected, errorKey);

        try
        {
            // Both scopes: the MSI/EXE API is only reachable with its own token.
            await TokenAsync(credentials, _options.StoreApiScope, cancellationToken);

            await SendAsync(Api.DevCenter, credentials, HttpMethod.Get, "applications?top=1", null, cancellationToken);

            return new StoreVerification(ConnectionStatus.Verified, null);
        }
        catch (StoreCallException ex) when (ex.StatusCode is 400 or 401 or 403 or 404)
        {
            return new StoreVerification(ConnectionStatus.Rejected, ex.Message);
        }
        catch (StoreCallException ex)
        {
            return new StoreVerification(ConnectionStatus.Unreachable, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MsApplicationRecord>> ListApplicationsAsync(string secretJson, CancellationToken cancellationToken)
    {
        var credentials = Credentials(secretJson);

        return [.. (await ListAllAsync(credentials, "applications?top=100", cancellationToken)).Select(ParseApplication)];
    }

    /// <inheritdoc/>
    public async Task<MsApplicationRecord> GetApplicationAsync(string secretJson, string applicationId, CancellationToken cancellationToken)
        => ParseApplication(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Get, $"applications/{Uri.EscapeDataString(applicationId)}", null, cancellationToken));

    #endregion

    #region Submissions

    /// <inheritdoc/>
    public async Task<JsonObject> GetSubmissionAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken)
        => AsObject(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Get, submission.Path(), null, cancellationToken));

    /// <inheritdoc/>
    /// <remarks>
    /// Sent without a request body, as the API documents. Partner Center reads a body (even an empty JSON object) as the
    /// content of the new submission and refuses it with 400 "The size of Listings must be 1 or more".
    /// </remarks>
    public async Task<JsonObject> CreateSubmissionAsync(string secretJson, MsSubmissionRef owner, CancellationToken cancellationToken)
        => AsObject(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Post, owner.WithSubmission(null).Path(), null, cancellationToken));

    /// <inheritdoc/>
    public async Task<JsonObject> UpdateSubmissionAsync(string secretJson, MsSubmissionRef submission, JsonObject content, CancellationToken cancellationToken)
        => AsObject(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Put, submission.Path(), content, cancellationToken));

    /// <inheritdoc/>
    /// <remarks>
    /// Sent without a request body, as the API documents (like <see cref="CreateSubmissionAsync"/>).
    /// </remarks>
    public async Task<MsStatusRecord> CommitSubmissionAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken)
        => ParseStatus(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Post, $"{submission.Path()}/commit", null, cancellationToken));

    /// <inheritdoc/>
    public async Task<MsStatusRecord> GetSubmissionStatusAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken)
        => ParseStatus(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Get, $"{submission.Path()}/status", null, cancellationToken));

    /// <inheritdoc/>
    public Task DeleteSubmissionAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken)
        => SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Delete, submission.Path(), null, cancellationToken);

    /// <inheritdoc/>
    public async Task<MsRolloutRecord> GetRolloutAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken)
        => ParseRollout(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Get, $"{submission.Path()}/packagerollout", null, cancellationToken));

    /// <inheritdoc/>
    public async Task<MsRolloutRecord> ChangeRolloutAsync(string secretJson, MsSubmissionRef submission, MsRolloutAction action, double? percentage, CancellationToken cancellationToken)
    {
        var path = action switch
        {
            MsRolloutAction.Halt => $"{submission.Path()}/haltpackagerollout",
            MsRolloutAction.Complete => $"{submission.Path()}/finalizepackagerollout",
            _ => $"{submission.Path()}/updatepackagerolloutpercentage?percentage={(percentage ?? 0).ToString("0.##", CultureInfo.InvariantCulture)}",
        };

        return ParseRollout(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Post, path, null, cancellationToken));
    }

    #endregion

    #region Flights and add-ons

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MsFlightRecord>> ListFlightsAsync(string secretJson, string applicationId, CancellationToken cancellationToken)
        => [.. (await ListAllAsync(Credentials(secretJson), $"applications/{Uri.EscapeDataString(applicationId)}/listflights?top=100", cancellationToken)).Select(ParseFlight)];

    /// <inheritdoc/>
    public async Task<MsFlightRecord> CreateFlightAsync(string secretJson, string applicationId, string friendlyName, IReadOnlyList<string> groupIds, string rankHigherThan, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["friendlyName"] = friendlyName,
            ["groupIds"] = new JsonArray([.. groupIds.Select(g => (JsonNode)JsonValue.Create(g))]),
        };

        if (!string.IsNullOrEmpty(rankHigherThan))
            body["rankHigherThan"] = rankHigherThan;

        return ParseFlight(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Post, $"applications/{Uri.EscapeDataString(applicationId)}/flights", body, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListAddonIdsAsync(string secretJson, string applicationId, CancellationToken cancellationToken)
        => [.. (await ListAllAsync(Credentials(secretJson), $"applications/{Uri.EscapeDataString(applicationId)}/listinappproducts?top=100", cancellationToken))
                .Select(a => MsSubmissionJson.String(a, "inAppProductId"))
                .Where(id => !string.IsNullOrEmpty(id))];

    /// <inheritdoc/>
    public async Task<MsAddonRecord> GetAddonAsync(string secretJson, string addonStoreId, CancellationToken cancellationToken)
        => ParseAddon(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Get, $"inappproducts/{Uri.EscapeDataString(addonStoreId)}", null, cancellationToken));

    /// <inheritdoc/>
    public async Task<MsAddonRecord> CreateAddonAsync(string secretJson, string applicationId, string productId, string productType, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["applicationIds"] = new JsonArray(JsonValue.Create(applicationId)),
            ["productId"] = productId,
            ["productType"] = productType,
        };

        return ParseAddon(await SendAsync(Api.DevCenter, Credentials(secretJson), HttpMethod.Post, "inappproducts", body, cancellationToken));
    }

    #endregion

    #region Files

    /// <inheritdoc/>
    public async Task UploadSubmissionFilesAsync(string fileUploadUrl, Stream zip, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(fileUploadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new StoreCallException("The submission has no valid file upload address.", null, false);

        using var response = await SendWithRetryAsync(() =>
        {
            if (zip.CanSeek)
                zip.Position = 0;

            var content = new StreamContent(zip);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = content };
            request.Headers.Add("x-ms-blob-type", "BlockBlob");

            return request;
        }, zip.CanSeek, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T> DownloadAsync<T>(string url, bool guardAddress, Func<Stream, long?, Task<T>> read, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && guardAddress))
            throw new StoreCallException("Only HTTPS addresses are downloaded.", null, false);

        if (guardAddress && _options.BlockPrivateDownloadAddresses)
            await DownloadGuard.EnsurePublicAsync(uri, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        HttpResponseMessage response;

        try
        {
            response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException(SecretRedactor.RedactAndTrim($"Download failed: {ex.Message}", _maxErrorLength), null, true);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new StoreCallException($"Download answered {(int)response.StatusCode}.", (int)response.StatusCode, (int)response.StatusCode >= 500);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            return await read(stream, response.Content.Headers.ContentLength);
        }
    }

    #endregion

    #region MSI/EXE products

    /// <inheritdoc/>
    public async Task<MsProductStatusRecord> GetProductStatusAsync(string secretJson, string productId, CancellationToken cancellationToken)
    {
        var envelope = await SendAsync(Api.StoreApi, Credentials(secretJson), HttpMethod.Get, ProductPath(productId, "status"), null, cancellationToken, unwrap: false);
        var data = envelope?["responseData"];

        return new MsProductStatusRecord(MsSubmissionJson.Bool(data, "isReady"), MsSubmissionJson.String(data, "ongoingSubmissionId"), MsSubmissionJson.ReadItems(envelope?["errors"]));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MsInstallerPackageRecord>> GetInstallerPackagesAsync(string secretJson, string productId, CancellationToken cancellationToken)
    {
        var data = await SendAsync(Api.StoreApi, Credentials(secretJson), HttpMethod.Get, ProductPath(productId, "packages"), null, cancellationToken);

        return [.. MsSubmissionJson.Array(data?["packages"]).Select(p => new MsInstallerPackageRecord
        {
            PackageId = MsSubmissionJson.String(p, "packageId"),
            PackageUrl = MsSubmissionJson.String(p, "packageUrl"),
            Languages = MsSubmissionJson.Strings(p["languages"]) ?? [],
            Architectures = MsSubmissionJson.Strings(p["architectures"]) ?? [],
            IsSilentInstall = MsSubmissionJson.Bool(p, "isSilentInstall"),
            InstallerParameters = MsSubmissionJson.String(p, "installerParameters"),
            GenericDocUrl = MsSubmissionJson.String(p, "genericDocUrl"),
            PackageType = string.Equals(MsSubmissionJson.String(p, "packageType"), "msi", StringComparison.OrdinalIgnoreCase) ? MsInstallerType.Msi : MsInstallerType.Exe,
        })];
    }

    /// <inheritdoc/>
    public Task ReplaceInstallerPackagesAsync(string secretJson, string productId, IReadOnlyList<MsInstallerPackageRecord> packages, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["packages"] = new JsonArray([.. packages.Select(p =>
            {
                var package = new JsonObject
                {
                    ["packageUrl"] = p.PackageUrl,
                    ["languages"] = new JsonArray([.. p.Languages.Select(l => (JsonNode)JsonValue.Create(l))]),
                    ["architectures"] = new JsonArray([.. p.Architectures.Select(a => (JsonNode)JsonValue.Create(a))]),
                    ["isSilentInstall"] = p.IsSilentInstall,
                    ["installerParameters"] = p.InstallerParameters ?? string.Empty,
                    ["packageType"] = p.PackageType == MsInstallerType.Msi ? "msi" : "exe",
                };

                if (!string.IsNullOrEmpty(p.GenericDocUrl))
                    package["genericDocUrl"] = p.GenericDocUrl;

                return (JsonNode)package;
            })]),
        };

        return SendAsync(Api.StoreApi, Credentials(secretJson), HttpMethod.Put, ProductPath(productId, "packages"), body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CommitInstallerPackagesAsync(string secretJson, string productId, CancellationToken cancellationToken)
        => SendAsync(Api.StoreApi, Credentials(secretJson), HttpMethod.Post, ProductPath(productId, "packages/commit"), new JsonObject(), cancellationToken);

    /// <inheritdoc/>
    public async Task<JsonObject> GetProductMetadataAsync(string secretJson, string productId, CancellationToken cancellationToken)
        => AsObject(await SendAsync(Api.StoreApi, Credentials(secretJson), HttpMethod.Get, ProductPath(productId, "metadata"), null, cancellationToken));

    /// <inheritdoc/>
    public Task UpdateProductMetadataAsync(string secretJson, string productId, JsonObject patch, CancellationToken cancellationToken)
        => SendAsync(Api.StoreApi, Credentials(secretJson), HttpMethod.Patch, ProductPath(productId, "metadata"), patch, cancellationToken);

    /// <inheritdoc/>
    public async Task<string> SubmitProductAsync(string secretJson, string productId, CancellationToken cancellationToken)
    {
        var data = await SendAsync(Api.StoreApi, Credentials(secretJson), HttpMethod.Post, ProductPath(productId, "submit"), new JsonObject(), cancellationToken);

        return MsSubmissionJson.String(data, "submissionId") ?? throw new StoreCallException("The submit answer has no submission id.", null, false);
    }

    /// <inheritdoc/>
    public async Task<MsPublishingStatusRecord> GetProductSubmissionStatusAsync(string secretJson, string productId, string submissionId, CancellationToken cancellationToken)
    {
        var data = await SendAsync(Api.StoreApi, Credentials(secretJson), HttpMethod.Get, ProductPath(productId, $"submission/{Uri.EscapeDataString(submissionId)}/status"), null, cancellationToken);

        return new MsPublishingStatusRecord(MsSubmissionJson.ParseStatus(MsSubmissionJson.String(data, "publishingStatus")), MsSubmissionJson.Bool(data, "hasFailed"));
    }

    #endregion

    #region Transport

    private static MicrosoftCredentials Credentials(string secretJson)
        => MicrosoftCredentialParser.Parse(secretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

    private static string ProductPath(string productId, string tail) => $"submission/v1/product/{Uri.EscapeDataString(productId)}/{tail}";

    private string BaseUrl(Api api) => (api == Api.DevCenter ? _options.DevCenterBaseUrl : _options.StoreApiBaseUrl).TrimEnd('/');

    private string Scope(Api api) => api == Api.DevCenter ? _options.DevCenterScope : _options.StoreApiScope;

    /// <summary>
    /// Follows <c>@nextLink</c> pages of a list and returns the items of <c>value</c>.
    /// </summary>
    private async Task<List<JsonNode>> ListAllAsync(MicrosoftCredentials credentials, string path, CancellationToken cancellationToken)
    {
        var items = new List<JsonNode>();

        for (var page = 0; path != null && page < 50; page++)
        {
            var document = await SendAsync(Api.DevCenter, credentials, HttpMethod.Get, path, null, cancellationToken);

            items.AddRange(MsSubmissionJson.Array(document?["value"]).Select(i => i.DeepClone()));

            path = MsSubmissionJson.String(document, "@nextLink");
        }

        return items;
    }

    /// <summary>
    /// Calls one API with a bearer token of its scope and parses the answer. The MSI/EXE API wraps answers in
    /// <c>{ isSuccess, errors, responseData }</c>: with <paramref name="unwrap"/> a false <c>isSuccess</c> throws and
    /// <c>responseData</c> is returned.
    /// </summary>
    private async Task<JsonNode> SendAsync(Api api, MicrosoftCredentials credentials, HttpMethod method, string path, JsonNode body, CancellationToken cancellationToken, bool unwrap = true)
    {
        var url = path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? path : $"{BaseUrl(api)}/{path.TrimStart('/')}";
        var scope = Scope(api);
        var key = MicrosoftTokenCache.Key(credentials, scope);
        var json = body?.ToJsonString();
        var token = await TokenAsync(credentials, scope, cancellationToken);

        for (var authAttempt = 1; ; authAttempt++)
        {
            var currentToken = token;

            try
            {
                using var response = await SendWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(method, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);

                    if (api == Api.StoreApi)
                        request.Headers.Add("X-Seller-Account-Id", credentials.SellerId);

                    if (json != null)
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    return request;
                }, true, cancellationToken);

                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                var document = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);

                if (api != Api.StoreApi || !unwrap)
                    return document;

                if (document is JsonObject envelope && envelope["isSuccess"] is JsonValue success && success.GetValueKind() == JsonValueKind.False)
                    throw new StoreCallException(SecretRedactor.RedactAndTrim($"{(int)response.StatusCode}: {DescribeError(text)}", _maxErrorLength), 400, false);

                return document?["responseData"] ?? document;
            }
            catch (StoreCallException ex) when (ex.StatusCode == 401 && authAttempt == 1)
            {
                // The cached token may have been revoked or the secret rotated: one more try with a fresh token.
                _tokenCache.Remove(key);
                token = await TokenAsync(credentials, scope, cancellationToken);
            }
        }
    }

    private Task<string> TokenAsync(MicrosoftCredentials credentials, string scope, CancellationToken cancellationToken)
        => _tokenCache.GetAsync(MicrosoftTokenCache.Key(credentials, scope), _timeProvider.GetUtcNow(), async () =>
        {
            var url = $"{_options.LoginBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(credentials.TenantId)}/oauth2/v2.0/token";

            using var response = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = credentials.ClientId,
                    ["client_secret"] = credentials.ClientSecret,
                    ["scope"] = scope,
                }),
            }, true, cancellationToken);

            var document = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var token = MsSubmissionJson.String(document, "access_token") ?? throw new StoreCallException("Token response has no access token.", null, false);
            var expiresIn = document?["expires_in"] is JsonValue value && value.TryGetValue<int>(out var seconds) ? seconds : 3599;

            return (token, _timeProvider.GetUtcNow().AddSeconds(expiresIn));
        }, cancellationToken);

    /// <summary>
    /// Sends the request <paramref name="build"/> makes, again on 429, 5xx and network failures while attempts remain.
    /// Returns a successful response (the caller disposes it); any other answer becomes a permanent
    /// <see cref="StoreCallException"/>.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> build, bool retryable, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var attempts = retryable ? Math.Max(1, _options.MaxAttempts) : 1;

        for (var attempt = 1; ; attempt++)
        {
            using var request = build();
            var path = request.RequestUri?.GetLeftPart(UriPartial.Path);

            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                if (attempt >= attempts)
                    throw new StoreCallException(SecretRedactor.RedactAndTrim($"Microsoft Store unreachable: {ex.Message}", _maxErrorLength), null, true);

                _logger?.Warning("Microsoft Store {Method} {Path} failed ({Error}); attempt {Attempt} of {Attempts}.", request.Method, path, SecretRedactor.Redact(ex.Message), attempt, attempts);

                await Task.Delay(Backoff(attempt, null), _timeProvider, cancellationToken);
                continue;
            }

            if (response.IsSuccessStatusCode)
                return response;

            var status = (int)response.StatusCode;
            var transient = response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500;

            if (transient && attempt < attempts)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;

                _logger?.Warning("Microsoft Store {Method} {Path} answered {Status}; attempt {Attempt} of {Attempts}.", request.Method, path, status, attempt, attempts);

                response.Dispose();

                await Task.Delay(Backoff(attempt, retryAfter), _timeProvider, cancellationToken);
                continue;
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger?.Warning("Microsoft Store {Method} {Path} answered {Status}.", request.Method, path, status);

                throw new StoreCallException(SecretRedactor.RedactAndTrim($"{status}: {DescribeError(body)}", _maxErrorLength), status, transient);
            }
        }
    }

    private TimeSpan Backoff(int attempt, TimeSpan? retryAfter)
    {
        var max = TimeSpan.FromSeconds(Math.Max(0, _options.MaxRetryDelaySeconds));

        if (retryAfter.HasValue)
            return retryAfter.Value < max ? retryAfter.Value : max;

        var delay = TimeSpan.FromMilliseconds(Math.Max(0, _options.RetryBaseDelayMilliseconds) * Math.Pow(2, attempt - 1));

        return delay < max ? delay : max;
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Extracts the readable part of a Microsoft error body: Entra (<c>error_description</c>), MSIX API (<c>message</c>,
    /// <c>error.message</c>) or MSI/EXE API (<c>errors[].message</c>); falls back to the raw text.
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public static string DescribeError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "empty response";

        try
        {
            var node = JsonNode.Parse(body);

            if (node is not JsonObject root)
                return body;

            if (MsSubmissionJson.String(root, "error_description") is { } description)
                return description;

            if (root["errors"] is JsonArray errors && errors.Count > 0)
                return string.Join("; ", errors.Select(e => $"{MsSubmissionJson.String(e, "code")} {MsSubmissionJson.String(e, "message")}".Trim()));

            if (MsSubmissionJson.String(root, "message") is { } message)
                return MsSubmissionJson.String(root, "code") is { } code ? $"{code} {message}" : message;

            if (root["error"] is JsonObject error)
                return MsSubmissionJson.String(error, "message") ?? MsSubmissionJson.String(error, "code") ?? error.ToJsonString();

            if (MsSubmissionJson.String(root, "error") is { } errorCode)
                return errorCode;
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }

    private static JsonObject AsObject(JsonNode node) => node as JsonObject ?? throw new StoreCallException("The store answered no object.", null, false);

    private static string SubmissionId(JsonNode node, string name) => node?[name] is JsonObject submission ? MsSubmissionJson.String(submission, "id") : null;

    private static MsApplicationRecord ParseApplication(JsonNode app) => new(MsSubmissionJson.String(app, "id"),
                                                                             MsSubmissionJson.String(app, "primaryName"),
                                                                             PackageFamilyName(app),
                                                                             SubmissionId(app, "lastPublishedApplicationSubmission"),
                                                                             SubmissionId(app, "pendingApplicationSubmission"));

    /// <summary>
    /// The package family name of an application resource: <c>packageFamilyName</c>, otherwise derived from
    /// <c>packageIdentityName</c> and <c>publisherName</c> (<see cref="MsixIdentity.FamilyName"/>), otherwise the bare
    /// identity name. Null when the resource reports no package identity.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static string PackageFamilyName(JsonNode app)
    {
        var familyName = MsSubmissionJson.String(app, "packageFamilyName");

        return string.IsNullOrWhiteSpace(familyName)
            ? MsixIdentity.FamilyName(MsSubmissionJson.String(app, "packageIdentityName"), MsSubmissionJson.String(app, "publisherName"))
            : familyName.Trim();
    }

    private static MsFlightRecord ParseFlight(JsonNode flight) => new(MsSubmissionJson.String(flight, "flightId"),
                                                                     MsSubmissionJson.String(flight, "friendlyName"),
                                                                     MsSubmissionJson.Strings(flight?["groupIds"]) ?? [],
                                                                     MsSubmissionJson.String(flight, "rankHigherThan"),
                                                                     SubmissionId(flight, "lastPublishedFlightSubmission"),
                                                                     SubmissionId(flight, "pendingFlightSubmission"));

    private static MsAddonRecord ParseAddon(JsonNode addon) => new(MsSubmissionJson.String(addon, "id"),
                                                                   MsSubmissionJson.String(addon, "productId"),
                                                                   MsSubmissionJson.String(addon, "productType"),
                                                                   SubmissionId(addon, "lastPublishedInAppProductSubmission"),
                                                                   SubmissionId(addon, "pendingInAppProductSubmission"));

    private static MsStatusRecord ParseStatus(JsonNode status) => new(MsSubmissionJson.ParseStatus(MsSubmissionJson.String(status, "status")), MsSubmissionJson.ReadStatusDetails(status?["statusDetails"]));

    private static MsRolloutRecord ParseRollout(JsonNode rollout) => new(MsSubmissionJson.Bool(rollout, "isPackageRollout"),
                                                                         MsSubmissionJson.Double(rollout, "packageRolloutPercentage") ?? 0,
                                                                         MsSubmissionJson.ParseRolloutStatus(MsSubmissionJson.String(rollout, "packageRolloutStatus")) ?? MsRolloutStatus.NotStarted,
                                                                         MsSubmissionJson.String(rollout, "fallbackSubmissionId"));

    #endregion
}
