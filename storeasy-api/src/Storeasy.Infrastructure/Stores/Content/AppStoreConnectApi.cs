using Storeasy.Application.Interfaces;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// A JSON:API client of App Store Connect for one set of credentials: ES256 tokens (renewed before they expire), paging
/// through <c>links.next</c> and the reserve / upload / commit protocol of screenshots, previews and attachments
/// (<c>uploadOperations</c> are presigned, so no token goes with them).
/// </summary>
/// <param name="http"></param>
/// <param name="credentials"></param>
public sealed class AppStoreConnectApi(StoreHttp http, AppleCredentials credentials)
{
    /// <summary>
    /// Most pages read from one list.
    /// </summary>
    public const int MaxPages = 20;

    private static readonly TimeSpan _tokenReuse = TimeSpan.FromMinutes(10);

    private readonly StoreHttp _http = http;
    private readonly AppleCredentials _credentials = credentials;

    private string _token;
    private DateTimeOffset _tokenDate;

    /// <summary>
    /// GET of one document.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken) => _http.SendJsonAsync(HttpMethod.Get, Url(path), Token(), null, true, cancellationToken);

    /// <summary>
    /// GET of one document; null when the store answers 404 (no such related resource).
    /// </summary>
    /// <param name="path"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<JsonDocument> GetOrNullAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var document = await GetAsync(path, cancellationToken);

            if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Null)
            {
                document.Dispose();
                return null;
            }

            return document;
        }
        catch (StoreCallException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// GET of a list, following <c>links.next</c>; returns the data and included resources (cloned).
    /// </summary>
    /// <param name="path"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(List<JsonElement> Data, List<JsonElement> Included)> GetAllAsync(string path, CancellationToken cancellationToken)
    {
        var data = new List<JsonElement>();
        var included = new List<JsonElement>();
        var url = path;

        for (var page = 0; url != null && page < MaxPages; page++)
        {
            using var document = await GetAsync(url, cancellationToken);

            data.AddRange(JsonRead.Items(document.RootElement, "data").Select(e => e.Clone()));
            included.AddRange(JsonRead.Items(document.RootElement, "included").Select(e => e.Clone()));

            url = JsonRead.Str(JsonRead.Get(document.RootElement, "links"), "next");
        }

        return (data, included);
    }

    /// <summary>
    /// POST; not retried unless <paramref name="retry"/> (a create that failed with 5xx may still have happened).
    /// </summary>
    /// <param name="path"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="retry"></param>
    /// <returns></returns>
    public Task<JsonDocument> PostAsync(string path, object body, CancellationToken cancellationToken, bool retry = false)
        => _http.SendJsonAsync(HttpMethod.Post, Url(path), Token(), body, retry, cancellationToken);

    /// <summary>
    /// PATCH (idempotent, retried).
    /// </summary>
    /// <param name="path"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<JsonDocument> PatchAsync(string path, object body, CancellationToken cancellationToken)
        => _http.SendJsonAsync(HttpMethod.Patch, Url(path), Token(), body, true, cancellationToken);

    /// <summary>
    /// DELETE (retried); a 404 counts as done.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var _ = await _http.SendJsonAsync(HttpMethod.Delete, Url(path), Token(), null, true, cancellationToken);
        }
        catch (StoreCallException ex) when (ex.StatusCode == 404)
        {
            // Already gone.
        }
    }

    /// <summary>
    /// Sends the byte ranges of <paramref name="data"/> to the addresses Apple reserved
    /// (<c>attributes.uploadOperations</c>).
    /// </summary>
    /// <param name="reservation"></param>
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task UploadAsync(JsonElement reservation, byte[] data, CancellationToken cancellationToken)
    {
        var operations = JsonRead.Items(JsonRead.Get(reservation, "attributes"), "uploadOperations").ToList();

        if (operations.Count == 0)
            throw new StoreCallException("Apple returned no upload operations for the reservation.", null, false);

        var covered = operations.Sum(o => (long)(JsonRead.Int(o, "length") ?? 0));

        if (covered != data.Length)
            throw new StoreCallException($"The upload operations cover {covered} bytes but the file has {data.Length}.", null, false);

        foreach (var operation in operations)
        {
            var offset = JsonRead.Int(operation, "offset") ?? 0;
            var length = JsonRead.Int(operation, "length") ?? 0;
            var method = new HttpMethod(JsonRead.Str(operation, "method") ?? "PUT");
            var url = JsonRead.Str(operation, "url");
            var headers = JsonRead.Items(operation, "requestHeaders").Select(h => (Name: JsonRead.Str(h, "name"), Value: JsonRead.Str(h, "value"))).ToList();

            await _http.SendAsync(() =>
            {
                var content = new ByteArrayContent(data, offset, length);
                var request = new HttpRequestMessage(method, url) { Content = content };

                foreach (var (name, value) in headers)
                {
                    if (!request.Headers.TryAddWithoutValidation(name, value))
                        content.Headers.TryAddWithoutValidation(name, value);
                }

                return request;
            }, true, cancellationToken);
        }
    }

    private static string Url(string path) => path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : StoreGateway.AppleBaseUrl + path;

    private string Token()
    {
        var now = _http.Clock.GetUtcNow();

        if (_token == null || now - _tokenDate > _tokenReuse)
        {
            _token = StoreJwt.CreateAppleToken(_credentials, now);
            _tokenDate = now;
        }

        return _token;
    }
}
