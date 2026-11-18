using Storeasy.Application.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// A Google Play Developer API (androidpublisher v3) client for one service account: the JWT bearer token exchange, the
/// edit lifecycle (insert, commit, delete) and media and resumable bundle uploads.
/// </summary>
/// <param name="http"></param>
/// <param name="credentials"></param>
/// <param name="packageName"></param>
public sealed class GooglePlayApi(StoreHttp http, GoogleCredentials credentials, string packageName)
{
    /// <summary>
    /// Upload base address of the Play Developer API.
    /// </summary>
    public const string UploadBaseUrl = "https://androidpublisher.googleapis.com/upload/androidpublisher/v3";

    private readonly StoreHttp _http = http;
    private readonly GoogleCredentials _credentials = credentials;
    private readonly string _appPath = $"/applications/{Uri.EscapeDataString(packageName)}";
    private readonly string _packageName = packageName;

    private string _token;

    /// <summary>
    /// Path of the app (<c>/applications/{package}</c>).
    /// </summary>
    public string AppPath => _appPath;

    /// <summary>
    /// Package name of the app.
    /// </summary>
    public string PackageName => _packageName;

    /// <summary>
    /// Opens an edit and returns its id.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> OpenEditAsync(CancellationToken cancellationToken)
    {
        using var edit = await SendAsync(HttpMethod.Post, $"{_appPath}/edits", "{}", cancellationToken);

        return JsonRead.Str(edit.RootElement, "id") ?? throw new StoreCallException("The edit response has no id.", null, false);
    }

    /// <summary>
    /// Commits an edit.
    /// </summary>
    /// <param name="editId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CommitEditAsync(string editId, CancellationToken cancellationToken)
    {
        using var _ = await SendAsync(HttpMethod.Post, $"{EditPath(editId)}:commit", null, cancellationToken);
    }

    /// <summary>
    /// Deletes an edit; failures are swallowed (the edit expires on its own) and reported as false.
    /// </summary>
    /// <param name="editId"></param>
    /// <returns></returns>
    public async Task<bool> DeleteEditAsync(string editId)
    {
        try
        {
            using var _ = await SendAsync(HttpMethod.Delete, EditPath(editId), null, CancellationToken.None, retry: true);
            return true;
        }
        catch (StoreCallException)
        {
            return false;
        }
    }

    /// <summary>
    /// Path of an edit.
    /// </summary>
    /// <param name="editId"></param>
    /// <returns></returns>
    public string EditPath(string editId) => $"{_appPath}/edits/{Uri.EscapeDataString(editId)}";

    /// <summary>
    /// Sends a JSON request to the API (path relative to the API base, or a full address).
    /// </summary>
    /// <param name="method"></param>
    /// <param name="path"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="retry"></param>
    /// <returns></returns>
    public async Task<JsonDocument> SendAsync(HttpMethod method, string path, object body, CancellationToken cancellationToken, bool? retry = null)
        => await _http.SendJsonAsync(method, Url(path), await TokenAsync(cancellationToken), body, retry, cancellationToken);

    /// <summary>
    /// Uploads one image (<c>uploadType=media</c>).
    /// </summary>
    /// <param name="path"></param>
    /// <param name="data"></param>
    /// <param name="contentType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<JsonDocument> UploadMediaAsync(string path, byte[] data, string contentType, CancellationToken cancellationToken)
    {
        var token = await TokenAsync(cancellationToken);

        var response = await _http.SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{UploadBaseUrl}{path}?uploadType=media") { Content = new ByteArrayContent(data) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");

            return request;
        }, true, cancellationToken);

        return StoreHttp.Parse(response);
    }

    /// <summary>
    /// Uploads a bundle with the resumable protocol: a session is opened, then chunks of
    /// <see cref="Application.Utils.Models.StoreApiOptions.UploadChunkBytes"/> are sent with <c>Content-Range</c>; the store
    /// answers 308 until the last chunk. Each chunk is retried on its own.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="content"></param>
    /// <param name="size"></param>
    /// <param name="progress">Called with the bytes sent so far.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<JsonDocument> UploadResumableAsync(string path, Stream content, long size, Func<long, Task> progress, CancellationToken cancellationToken)
    {
        var token = await TokenAsync(cancellationToken);

        var session = await _http.SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{UploadBaseUrl}{path}?uploadType=resumable");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("X-Upload-Content-Type", "application/octet-stream");
            request.Headers.TryAddWithoutValidation("X-Upload-Content-Length", size.ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            return request;
        }, true, cancellationToken);

        var sessionUrl = session.Location ?? throw new StoreCallException("Play did not open an upload session.", null, false);
        var chunkSize = Math.Max(256 * 1024, _http.Options.UploadChunkBytes / (256 * 1024) * (256 * 1024));
        var buffer = new byte[chunkSize];
        long sent = 0;

        while (true)
        {
            var read = await ReadChunkAsync(content, buffer, cancellationToken);
            var start = sent;
            var end = sent + read - 1;
            var chunk = buffer;
            var length = read;

            var response = await _http.SendAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, sessionUrl) { Content = new ByteArrayContent(chunk, 0, length) };
                request.Content.Headers.ContentRange = length == 0 ? new ContentRangeHeaderValue(size) : new ContentRangeHeaderValue(start, end, size);

                return request;
            }, true, cancellationToken, acceptStatus: 308);

            sent += read;

            await progress(sent);

            if (response.Status != 308)
                return StoreHttp.Parse(response);

            if (read == 0)
                throw new StoreCallException("Play still waits for bundle bytes after the whole file was sent.", null, false);
        }
    }

    private static async Task<int> ReadChunkAsync(Stream content, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await content.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);

            if (read == 0)
                break;

            total += read;
        }

        return total;
    }

    private static string Url(string path) => path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : StoreGateway.GoogleBaseUrl + path;

    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        if (_token != null)
            return _token;

        var assertion = StoreJwt.CreateGoogleAssertion(_credentials, StoreGateway.GoogleScope, _http.Clock.GetUtcNow());

        var response = await _http.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, _credentials.TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }),
        }, true, cancellationToken);

        using var document = StoreHttp.Parse(response);

        _token = JsonRead.Str(document.RootElement, "access_token") ?? throw new StoreCallException("Token response has no access token.", null, false);

        return _token;
    }
}
