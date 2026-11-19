using Microsoft.Extensions.Logging;
using SkiaSharp;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Ai;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Ai;

/// <summary>
/// Default <see cref="IPreReviewImageLoader"/>: downloads screenshots from the stores' public image hosts only (HTTPS,
/// App Store and Google Play image hosts), refuses large files, scales them so the longest edge is at most
/// <see cref="AiOptions.PreReviewImageMaxEdge"/> pixels and encodes them as JPEG. The images go to Anthropic as base64,
/// because a store address may not be reachable from there. An image that cannot be loaded is skipped.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public sealed class PreReviewImageLoader(IHttpClientFactory httpClientFactory, AiOptions options, ILogger<PreReviewImageLoader> logger) : IPreReviewImageLoader
{
    /// <summary>
    /// Name of the HTTP client the images are downloaded with.
    /// </summary>
    public const string HttpClientName = "pre-review-images";

    /// <summary>
    /// Largest image downloaded, in bytes.
    /// </summary>
    public const int MaxDownloadBytes = 10 * 1024 * 1024;

    private const int _jpegQuality = 80;

    private static readonly string[] _allowedHostSuffixes = [".mzstatic.com", ".googleusercontent.com", ".ggpht.com"];

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly AiOptions _options = options;
    private readonly ILogger<PreReviewImageLoader> _logger = logger;

    /// <inheritdoc/>
    public async Task<List<PreReviewImage>> LoadAsync(IReadOnlyList<PreReviewImageCandidate> candidates, int max, CancellationToken cancellationToken = default)
    {
        var images = new List<PreReviewImage>();

        if (candidates == null || max <= 0)
            return images;

        var client = _httpClientFactory.CreateClient(HttpClientName);

        foreach (var candidate in candidates)
        {
            if (images.Count >= max)
                break;

            if (!IsAllowed(candidate.Url))
                continue;

            var bytes = await DownloadAsync(client, candidate.Url, cancellationToken);
            var jpeg = bytes == null ? null : Downscale(bytes, _options.PreReviewImageMaxEdge);

            if (jpeg != null)
                images.Add(new PreReviewImage(candidate.Label, "image/jpeg", jpeg, candidate.Checksum));
        }

        return images;
    }

    /// <summary>
    /// Whether <paramref name="url"/> is an HTTPS address on a store image host.
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static bool IsAllowed(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && _allowedHostSuffixes.Any(suffix => uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <paramref name="bytes"/> decoded, scaled so its longest edge is at most <paramref name="maxEdge"/> and encoded as
    /// JPEG; null when it is not an image.
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="maxEdge"></param>
    /// <returns></returns>
    public static byte[] Downscale(byte[] bytes, int maxEdge)
    {
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream);

        if (codec == null)
            return null;

        using var source = SKBitmap.Decode(codec);

        if (source == null || source.Width <= 0 || source.Height <= 0)
            return null;

        var scale = Math.Min(1d, (double)maxEdge / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var scaled = scale < 1d ? source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell)) : null;
        using var image = SKImage.FromBitmap(scaled ?? source);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, _jpegQuality);

        return data?.ToArray();
    }

    private async Task<byte[]> DownloadAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxDownloadBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxDownloadBytes)
                    return null;

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("A screenshot for the pre-review could not be downloaded ({ErrorType}).", exception.GetType().Name);

            return null;
        }
    }
}
