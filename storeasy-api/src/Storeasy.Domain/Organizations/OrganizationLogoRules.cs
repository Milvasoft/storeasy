namespace Storeasy.Domain.Organizations;

/// <summary>
/// Pure rules of organization logos: accepted types, size limits, the file signature check and the object key of the
/// stored copy. No storage call and no image decoding (the API redraws the image through <c>ILogoImageNormalizer</c>).
/// </summary>
public static class OrganizationLogoRules
{
    /// <summary>
    /// Largest logo file accepted, in bytes (2 MB).
    /// </summary>
    public const long MaxBytes = 2L * 1024 * 1024;

    /// <summary>
    /// Width and height of the stored logo, in pixels. A smaller square is kept at its size.
    /// </summary>
    public const int OutputSize = 256;

    /// <summary>
    /// Largest width or height of an uploaded image, in pixels (a guard against images that decode to huge bitmaps).
    /// </summary>
    public const int MaxSourceDimension = 8192;

    /// <summary>
    /// Smallest width or height of an uploaded image, in pixels.
    /// </summary>
    public const int MinSourceDimension = 16;

    /// <summary>
    /// Content type of the stored logo.
    /// </summary>
    public const string OutputContentType = "image/png";

    /// <summary>
    /// Column length of <see cref="Organization.LogoKey"/>.
    /// </summary>
    public const int ObjectKeyMaxLength = 512;

    /// <summary>
    /// How long a logo URL handed to a client stays valid. Clients read the organization list again well within it.
    /// </summary>
    public static readonly TimeSpan UrlLifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// Content types an upload may declare. SVG is not accepted: it can carry script and is not redrawn here.
    /// </summary>
    public static readonly string[] AcceptedContentTypes = ["image/png", "image/jpeg", "image/webp"];

    private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Content type of <paramref name="data"/> by its file signature (PNG, JPEG or WebP); null for anything else,
    /// whatever its name or declared type says.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[..8].SequenceEqual(_pngSignature))
            return "image/png";

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";

        return null;
    }

    /// <summary>
    /// Whether <paramref name="contentType"/> is one of <see cref="AcceptedContentTypes"/> (case insensitive).
    /// </summary>
    /// <param name="contentType"></param>
    /// <returns></returns>
    public static bool IsAcceptedContentType(string contentType)
        => contentType != null && AcceptedContentTypes.Contains(contentType.Trim().ToLowerInvariant());

    /// <summary>
    /// Object key of a stored logo: <c>org/{organizationId}/logo/{nonce}.png</c>. The nonce is fresh per logo, so the
    /// URL of a replaced logo changes and no cache shows the old one.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="nonce"></param>
    /// <returns></returns>
    public static string ObjectKey(int organizationId, Guid nonce) => $"org/{organizationId}/logo/{nonce:N}.png";
}
