using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Intake;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Storeasy.Infrastructure.Intake;

/// <summary>
/// Downloads a pulled package (EAS, generic webhook) into the bucket, safe against server side request forgery: https
/// only, every URL of the redirect chain must be on the source's allowed domains (redirects are followed by hand), and the
/// connection refuses hosts that resolve to loopback, private, link local or other internal addresses (checked at connect
/// time, so DNS rebinding cannot slip past). The body streams into a multipart upload with a SHA-256 computed on the way
/// and a size limit.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="fileStorage"></param>
/// <param name="artifactStorage"></param>
/// <param name="options"></param>
public sealed class ArtifactDownloader(IHttpClientFactory httpClientFactory, IFileStorage fileStorage, IArtifactStorage artifactStorage, IntakeOptions options)
{
    /// <summary>
    /// Name of the download HTTP client (tests replace its handler).
    /// </summary>
    public const string HttpClientName = "artifact-download";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IArtifactStorage _artifactStorage = artifactStorage;
    private readonly IntakeOptions _options = options;

    /// <summary>
    /// The primary handler of the download client: no proxy, no automatic redirects, internal addresses refused.
    /// </summary>
    /// <returns></returns>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(30),
        ConnectCallback = ConnectAsync,
    };

    /// <summary>
    /// Connects only when every address of the host is public.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await ResolveAsync(context.DnsEndPoint.Host, cancellationToken);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Resolves <paramref name="host"/> and refuses it when any address is internal.
    /// </summary>
    /// <param name="host"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(IntakeRules.IsBlockedAddress))
            throw new HttpRequestException($"Downloads from {host} are not allowed (internal address).");

        return addresses;
    }

    /// <summary>
    /// Downloads <paramref name="url"/> into <paramref name="objectKey"/> and returns its size and SHA-256.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="allowedDomains"></param>
    /// <param name="objectKey"></param>
    /// <param name="maxBytes"></param>
    /// <param name="onProgress">Called with the bytes received so far.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(long Size, string Sha256)> DownloadAsync(string url, IReadOnlyCollection<string> allowedDomains, string objectKey, long maxBytes, Func<long, Task> onProgress, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var current = CheckUrl(url, allowedDomains);
        HttpResponseMessage response = null;

        try
        {
            for (var hop = 0; ; hop++)
            {
                response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, current), HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location != null)
                {
                    if (hop >= _options.MaxRedirects)
                        throw new ArtifactDownloadException("Too many redirects.");

                    current = CheckUrl(new Uri(current, response.Headers.Location).ToString(), allowedDomains);
                    response.Dispose();
                    response = null;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new ArtifactDownloadException($"The download answered {(int)response.StatusCode}.");

                break;
            }

            if (response.Content.Headers.ContentLength > maxBytes)
                throw new ArtifactDownloadException($"The package is larger than the limit ({maxBytes} bytes).");

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);

            return await StoreAsync(body, objectKey, maxBytes, onProgress, cancellationToken);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private async Task<(long Size, string Sha256)> StoreAsync(Stream body, string objectKey, long maxBytes, Func<long, Task> onProgress, CancellationToken cancellationToken)
    {
        var partSize = (int)IntakeRules.DefaultPartSize * 2;
        var uploadId = await _fileStorage.StartMultipartUploadAsync(objectKey, "application/octet-stream", cancellationToken);
        var parts = new List<UploadedPart>();
        var buffer = new byte[partSize];
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;

        try
        {
            while (true)
            {
                var filled = 0;

                while (filled < partSize)
                {
                    var read = await body.ReadAsync(buffer.AsMemory(filled, partSize - filled), cancellationToken);

                    if (read == 0)
                        break;

                    filled += read;
                }

                if (filled == 0)
                    break;

                total += filled;

                if (total > maxBytes)
                    throw new ArtifactDownloadException($"The package is larger than the limit ({maxBytes} bytes).");

                sha.AppendData(buffer, 0, filled);

                using (var part = new MemoryStream(buffer, 0, filled, writable: false))
                {
                    var etag = await _artifactStorage.UploadPartAsync(objectKey, uploadId, parts.Count + 1, part, filled, cancellationToken);

                    parts.Add(new UploadedPart(parts.Count + 1, etag));
                }

                if (onProgress != null)
                    await onProgress(total);

                if (filled < partSize)
                    break;
            }

            if (total == 0)
                throw new ArtifactDownloadException("The download was empty.");

            await _fileStorage.CompleteMultipartUploadAsync(objectKey, uploadId, parts, cancellationToken);
        }
        catch
        {
            await _fileStorage.AbortMultipartUploadAsync(objectKey, uploadId, CancellationToken.None);
            throw;
        }

        return (total, Convert.ToHexStringLower(sha.GetHashAndReset()));
    }

    private static Uri CheckUrl(string url, IReadOnlyCollection<string> allowedDomains)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArtifactDownloadException("Only https download addresses are accepted.");

        if (!IntakeRules.IsHostAllowed(uri.Host, allowedDomains) || IPAddress.TryParse(uri.Host, out _))
            throw new ArtifactDownloadException($"{SecretRedactor.Redact(uri.Host)} is not an allowed download domain.");

        return uri;
    }
}

/// <summary>
/// A pulled package could not be downloaded. The message is safe to store and show (no query string).
/// </summary>
/// <param name="message"></param>
public sealed class ArtifactDownloadException(string message) : Exception(message)
{
}
