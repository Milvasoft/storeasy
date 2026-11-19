using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores;

/// <summary>
/// App, build and icon reads of the store engine (the sync job): App Store Connect <c>/v1/apps</c> and <c>/v1/builds</c>,
/// Google Play edits (Play has no list-apps API, so an app is read by package name inside a short lived edit that is
/// deleted afterwards) and, for Microsoft Store icons only, the public display catalog. Failures become
/// <see cref="StoreCallException"/>.
/// </summary>
public sealed partial class StoreGateway
{
    /// <summary>
    /// Most pages read from one paginated App Store Connect list.
    /// </summary>
    public const int MaxPages = 10;

    /// <summary>
    /// Builds read for the icon of an App Store Connect app: the newest ones, newest first.
    /// </summary>
    public const int AppleIconBuilds = 5;

    /// <summary>
    /// Side, in pixels, of the icon asked from Apple's image service.
    /// </summary>
    public const int AppleIconSize = 512;

    /// <summary>
    /// Public Microsoft Store display catalog, read without credentials for the logo of a published product.
    /// </summary>
    public const string MicrosoftCatalogBaseUrl = "https://displaycatalog.mp.microsoft.com";

    /// <summary>
    /// Time allowed for one display catalog read, shorter than the store client's own timeout.
    /// </summary>
    public static readonly TimeSpan MicrosoftCatalogTimeout = TimeSpan.FromSeconds(20);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreAppRecord>> ListAppsAsync(StoreKind store, string secretJson, CancellationToken cancellationToken)
    {
        if (store == StoreKind.Microsoft && _microsoftStore != null)
            return [.. (await _microsoftStore.ListApplicationsAsync(secretJson, cancellationToken)).Select(a => new StoreAppRecord(a.Id, a.PackageFamilyName, a.PrimaryName))];

        if (store != StoreKind.Apple)
            throw new StoreCallException(store == StoreKind.Google ? "Google Play has no API that lists apps; add the app by its package name." : NotSupportedMessage(store), null, false);

        var token = AppleToken(secretJson);
        var apps = new List<StoreAppRecord>();
        var url = $"{AppleBaseUrl}/v1/apps?limit=200&fields[apps]=name,bundleId";

        for (var page = 0; url != null && page < MaxPages; page++)
        {
            using var document = await SendAsync(HttpMethod.Get, url, token, cancellationToken);

            apps.AddRange(document.RootElement.GetProperty("data").EnumerateArray().Select(ParseAppleApp));

            url = NextLink(document.RootElement);
        }

        return apps;
    }

    /// <inheritdoc/>
    public async Task<StoreAppRecord> GetAppAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken)
    {
        switch (store)
        {
            case StoreKind.Apple:
            {
                using var document = await SendAsync(HttpMethod.Get, $"{AppleBaseUrl}/v1/apps/{Uri.EscapeDataString(externalId)}?fields[apps]=name,bundleId", AppleToken(secretJson), cancellationToken);

                return ParseAppleApp(document.RootElement.GetProperty("data"));
            }
            case StoreKind.Google:
                return await WithGoogleEditAsync(secretJson, externalId, async (token, editUrl) =>
                {
                    using var details = await SendAsync(HttpMethod.Get, $"{editUrl}/details", token, cancellationToken);

                    var language = ReadString(details.RootElement, "defaultLanguage") ?? "en-US";

                    using var listing = await SendAsync(HttpMethod.Get, $"{editUrl}/listings/{Uri.EscapeDataString(language)}", token, cancellationToken);

                    return new StoreAppRecord(externalId, externalId, ReadString(listing.RootElement, "title") ?? externalId);
                }, cancellationToken);
            case StoreKind.Microsoft when _microsoftStore != null:
            {
                var app = await _microsoftStore.GetApplicationAsync(secretJson, externalId, cancellationToken);

                return new StoreAppRecord(app.Id, app.PackageFamilyName, app.PrimaryName);
            }
            default:
                throw new StoreCallException(NotSupportedMessage(store), null, false);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreBuildRecord>> ListBuildsAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken)
    {
        switch (store)
        {
            case StoreKind.Apple:
                return await ListAppleBuildsAsync(AppleToken(secretJson), externalId, cancellationToken);
            case StoreKind.Google:
                return await WithGoogleEditAsync(secretJson, externalId, async (token, editUrl) =>
                {
                    using var bundles = await SendAsync(HttpMethod.Get, $"{editUrl}/bundles", token, cancellationToken);
                    using var tracks = await SendAsync(HttpMethod.Get, $"{editUrl}/tracks", token, cancellationToken);

                    return ParseGoogleBuilds(bundles.RootElement, tracks.RootElement);
                }, cancellationToken);
            case StoreKind.Microsoft when _microsoftStore != null:
            {
                var app = await _microsoftStore.GetApplicationAsync(secretJson, externalId, cancellationToken);

                return (await Microsoft.MsSyncReader.ReadAppAsync(_microsoftStore, secretJson, app, cancellationToken)).Builds;
            }
            default:
                throw new StoreCallException(NotSupportedMessage(store), null, false);
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetIconUrlAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken)
    {
        switch (store)
        {
            case StoreKind.Apple:
            {
                // A build that is still processing has no icon asset yet, so a few of the newest builds are read.
                var url = $"{AppleBaseUrl}/v1/builds?filter[app]={Uri.EscapeDataString(externalId)}&sort=-uploadedDate&limit={AppleIconBuilds}&fields[builds]=iconAssetToken,uploadedDate";

                using var document = await SendAsync(HttpMethod.Get, url, AppleToken(secretJson), cancellationToken);

                return ParseAppleIconUrl(document.RootElement);
            }
            case StoreKind.Google:
                return await WithGoogleEditAsync(secretJson, externalId, async (token, editUrl) =>
                {
                    using var details = await SendAsync(HttpMethod.Get, $"{editUrl}/details", token, cancellationToken);

                    var language = ReadString(details.RootElement, "defaultLanguage") ?? "en-US";

                    using var images = await SendAsync(HttpMethod.Get, $"{editUrl}/listings/{Uri.EscapeDataString(language)}/icon", token, cancellationToken);

                    return ParseGoogleIconUrl(images.RootElement);
                }, cancellationToken);
            case StoreKind.Microsoft:
                return await GetMicrosoftIconUrlAsync(externalId, cancellationToken);
            default:
                return null;
        }
    }

    /// <summary>
    /// Reads the logo of a Microsoft Store product from the public display catalog (no credentials). A product the catalog
    /// does not list (not published yet, or not in the US market) has no icon: null, not an error. The call has its own
    /// <see cref="MicrosoftCatalogTimeout"/>; a timeout, 429 or 5xx is a transient <see cref="StoreCallException"/>.
    /// </summary>
    private async Task<string> GetMicrosoftIconUrlAsync(string productId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productId))
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(MicrosoftCatalogTimeout);

        try
        {
            using var document = await SendRequestAsync(new HttpRequestMessage(HttpMethod.Get, MicrosoftCatalogUrl(productId)), timeout.Token);

            return ParseMicrosoftIconUrl(document.RootElement);
        }
        catch (StoreCallException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StoreCallException("The Microsoft Store catalog did not answer in time.", null, true);
        }
    }

    /// <summary>
    /// Display catalog address of one product (US market, English).
    /// </summary>
    public static string MicrosoftCatalogUrl(string productId)
        => $"{MicrosoftCatalogBaseUrl}/v7.0/products?bigIds={Uri.EscapeDataString(productId)}&market=US&languages=en-us";

    /// <summary>
    /// Takes the logo of the first product of a display catalog answer (<c>Products[].LocalizedProperties[].Images[]</c>):
    /// the largest square image whose <c>ImagePurpose</c> is <c>Logo</c>, else the largest square <c>Tile</c>, else the
    /// largest square <c>BoxArt</c>. The catalog gives protocol relative addresses (<c>//store-images...</c>), which become
    /// https. Null when there is no product or no such image, or the result is not an absolute https address.
    /// </summary>
    public static string ParseMicrosoftIconUrl(JsonElement root)
    {
        var product = Items(root, "Products").FirstOrDefault();

        if (product.ValueKind != JsonValueKind.Object)
            return null;

        var images = Items(product, "LocalizedProperties").SelectMany(l => Items(l, "Images"))
                                                          .Select(i => (Purpose: ReadString(i, "ImagePurpose"), Width: ReadInt(i, "Width"), Height: ReadInt(i, "Height"), Uri: ReadString(i, "Uri")))
                                                          .Where(i => i.Width > 0 && i.Width == i.Height && !string.IsNullOrWhiteSpace(i.Uri))
                                                          .ToList();

        foreach (var purpose in _microsoftIconPurposes)
        {
            var best = images.Where(i => string.Equals(i.Purpose, purpose, StringComparison.OrdinalIgnoreCase))
                             .OrderByDescending(i => i.Width)
                             .Select(i => HttpsOrNull(i.Uri.StartsWith("//", StringComparison.Ordinal) ? $"https:{i.Uri}" : i.Uri))
                             .FirstOrDefault(u => u != null);

            if (best != null)
                return best;
        }

        return null;
    }

    private static readonly string[] _microsoftIconPurposes = ["Logo", "Tile", "BoxArt"];

    private static int ReadInt(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

    /// <summary>
    /// Takes the icon of the first build in a <c>/v1/builds</c> page whose <c>iconAssetToken</c> has a template address and
    /// fills the template (<c>{w}</c>, <c>{h}</c>, <c>{f}</c>) for a <see cref="AppleIconSize"/> square png. Null when no
    /// build has one or the result is not an absolute https address.
    /// </summary>
    public static string ParseAppleIconUrl(JsonElement root)
    {
        foreach (var build in Items(root, "data"))
        {
            var template = build.TryGetProperty("attributes", out var attributes)
                           && attributes.TryGetProperty("iconAssetToken", out var asset)
                ? ReadString(asset, "templateUrl")
                : null;

            if (string.IsNullOrWhiteSpace(template))
                continue;

            var size = AppleIconSize.ToString(CultureInfo.InvariantCulture);

            return HttpsOrNull(template.Replace("{w}", size).Replace("{h}", size).Replace("{f}", "png"));
        }

        return null;
    }

    /// <summary>
    /// Takes the first image of a Play listing images list (<c>images[].url</c>). Null when there is none or it is not an
    /// absolute https address.
    /// </summary>
    public static string ParseGoogleIconUrl(JsonElement root) => HttpsOrNull(Items(root, "images").Select(i => ReadString(i, "url")).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)));

    private static string HttpsOrNull(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : null;

    private async Task<IReadOnlyList<StoreBuildRecord>> ListAppleBuildsAsync(string token, string appId, CancellationToken cancellationToken)
    {
        var builds = new List<StoreBuildRecord>();
        var url = $"{AppleBaseUrl}/v1/builds?filter[app]={Uri.EscapeDataString(appId)}&limit=200&sort=-uploadedDate&include=preReleaseVersion"
                  + "&fields[builds]=version,uploadedDate,expirationDate,expired,processingState,minOsVersion,preReleaseVersion&fields[preReleaseVersions]=version,platform";

        for (var page = 0; url != null && page < MaxPages; page++)
        {
            using var document = await SendAsync(HttpMethod.Get, url, token, cancellationToken);

            builds.AddRange(ParseAppleBuilds(document.RootElement));

            url = NextLink(document.RootElement);
        }

        return builds;
    }

    /// <summary>
    /// Maps one page of <c>/v1/builds</c> (with the included pre-release versions) to build records.
    /// </summary>
    public static List<StoreBuildRecord> ParseAppleBuilds(JsonElement root)
    {
        var versions = Items(root, "included").Where(i => ReadString(i, "type") == "preReleaseVersions")
                                              .GroupBy(i => ReadString(i, "id"))
                                              .ToDictionary(g => g.Key, g => ReadString(g.First().GetProperty("attributes"), "version"));

        return [.. root.GetProperty("data").EnumerateArray().Select(build =>
        {
            var attributes = build.GetProperty("attributes");
            var versionId = build.TryGetProperty("relationships", out var relationships)
                            && relationships.TryGetProperty("preReleaseVersion", out var preRelease)
                            && preRelease.TryGetProperty("data", out var data)
                            && data.ValueKind == JsonValueKind.Object
                ? ReadString(data, "id")
                : null;

            var expired = attributes.TryGetProperty("expired", out var expiredValue) && expiredValue.ValueKind == JsonValueKind.True;

            return new StoreBuildRecord(ReadString(build, "id"),
                                        versionId != null && versions.TryGetValue(versionId, out var version) ? version : null,
                                        ReadString(attributes, "version"),
                                        expired ? BuildProcessingState.Expired : MapAppleState(ReadString(attributes, "processingState")),
                                        ReadDate(attributes, "uploadedDate"),
                                        ReadDate(attributes, "expirationDate"),
                                        null,
                                        ReadString(attributes, "minOsVersion"));
        })];
    }

    /// <summary>
    /// Maps the bundles of a Play edit to build records; the version name and the tracks come from the track releases
    /// that carry the version code.
    /// </summary>
    public static List<StoreBuildRecord> ParseGoogleBuilds(JsonElement bundles, JsonElement tracks)
    {
        var releases = Items(tracks, "tracks").SelectMany(track => Items(track, "releases").SelectMany(release => Items(release, "versionCodes")
                                                  .Select(code => (Code: code.ToString(), Track: ReadString(track, "track"), Name: ReadString(release, "name")))))
                                              .ToList();

        return [.. Items(bundles, "bundles").Select(bundle => bundle.GetProperty("versionCode").ToString())
                             .Distinct()
                             .OrderByDescending(code => long.TryParse(code, out var number) ? number : 0)
                             .Select(code =>
                             {
                                 var matches = releases.Where(r => r.Code == code).ToList();
                                 var tracksText = string.Join(",", matches.Select(r => r.Track).Where(t => t != null).Distinct());

                                 return new StoreBuildRecord(code,
                                                             matches.Select(r => r.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                                                             code,
                                                             BuildProcessingState.Valid,
                                                             null,
                                                             null,
                                                             tracksText.Length == 0 ? null : tracksText,
                                                             null);
                             })];
    }

    private async Task<T> WithGoogleEditAsync<T>(string secretJson, string packageName, Func<string, string, Task<T>> work, CancellationToken cancellationToken)
    {
        var credentials = StoreCredentialParser.ParseGoogle(secretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);
        var token = await GoogleAccessTokenAsync(credentials, cancellationToken);
        var appUrl = $"{GoogleBaseUrl}/applications/{Uri.EscapeDataString(packageName)}";

        string editId;

        using (var edit = await SendAsync(HttpMethod.Post, $"{appUrl}/edits", token, cancellationToken, "{}"))
        {
            editId = ReadString(edit.RootElement, "id") ?? throw new StoreCallException("The edit response has no id.", null, false);
        }

        var editUrl = $"{appUrl}/edits/{Uri.EscapeDataString(editId)}";

        try
        {
            return await work(token, editUrl);
        }
        finally
        {
            // The edit only read; leaving it would block the next one until it expires.
            try
            {
                using var deleted = await SendAsync(HttpMethod.Delete, editUrl, token, CancellationToken.None);
            }
            catch (StoreCallException ex)
            {
                _logger?.Warning("A Play edit of {PackageName} could not be deleted: {Error}", packageName, ex.Message);
            }
        }
    }

    private async Task<string> GoogleAccessTokenAsync(GoogleCredentials credentials, CancellationToken cancellationToken)
    {
        var assertion = StoreJwt.CreateGoogleAssertion(credentials, GoogleScope, _timeProvider.GetUtcNow());

        using var request = new HttpRequestMessage(HttpMethod.Post, credentials.TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }),
        };

        using var document = await SendRequestAsync(request, cancellationToken);

        return ReadString(document.RootElement, "access_token") ?? throw new StoreCallException("Token response has no access token.", null, false);
    }

    private string AppleToken(string secretJson)
    {
        var credentials = StoreCredentialParser.ParseApple(secretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

        return StoreJwt.CreateAppleToken(credentials, _timeProvider.GetUtcNow());
    }

    private Task<JsonDocument> SendAsync(HttpMethod method, string url, string bearerToken, CancellationToken cancellationToken, string jsonBody = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        if (jsonBody != null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        return SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends <paramref name="request"/> (and disposes it) and parses the JSON answer; an empty answer parses as <c>{}</c>.
    /// </summary>
    private async Task<JsonDocument> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            HttpResponseMessage response;

            try
            {
                response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new StoreCallException(SecretRedactor.RedactAndTrim($"Store unreachable: {ex.Message}", _maxErrorLength), null, true);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new StoreCallException("The store did not answer in time.", null, true);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var status = (int)response.StatusCode;
                    var transient = response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500;

                    throw new StoreCallException(SecretRedactor.RedactAndTrim($"{status}: {DescribeError(body)}", _maxErrorLength), status, transient);
                }

                return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            }
        }
    }

    private static StoreAppRecord ParseAppleApp(JsonElement app)
    {
        var attributes = app.GetProperty("attributes");

        return new StoreAppRecord(ReadString(app, "id"), ReadString(attributes, "bundleId"), ReadString(attributes, "name"));
    }

    private static IEnumerable<JsonElement> Items(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static string NextLink(JsonElement root) => root.TryGetProperty("links", out var links) ? ReadString(links, "next") : null;

    private static BuildProcessingState MapAppleState(string state) => state switch
    {
        "VALID" => BuildProcessingState.Valid,
        "FAILED" => BuildProcessingState.Failed,
        "INVALID" => BuildProcessingState.Invalid,
        _ => BuildProcessingState.Processing,
    };

    private static string ReadString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTime? ReadDate(JsonElement element, string name)
        => DateTimeOffset.TryParse(ReadString(element, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value.UtcDateTime : null;
}
