using Storeasy.Domain.Enums;
using System.Text;

namespace Storeasy.Domain.Assets;

/// <summary>
/// Pure rules of stored files: size limits, object keys and the multipart split. No storage call.
/// </summary>
public static class AssetRules
{
    /// <summary>
    /// Files larger than this are uploaded in parts.
    /// </summary>
    public const long MultipartThreshold = 64L * 1024 * 1024;

    /// <summary>
    /// Smallest part S3 accepts (every part but the last).
    /// </summary>
    public const long MinPartSize = 5L * 1024 * 1024;

    /// <summary>
    /// Part size used when the file needs no bigger parts.
    /// </summary>
    public const long DefaultPartSize = 16L * 1024 * 1024;

    /// <summary>
    /// Most parts S3 accepts in one upload.
    /// </summary>
    public const int MaxPartCount = 10_000;

    private const int _maxKeyFileNameLength = 100;

    /// <summary>
    /// Largest file accepted for <paramref name="purpose"/>, in bytes.
    /// </summary>
    /// <param name="purpose"></param>
    /// <returns></returns>
    public static long MaxBytes(AssetPurpose purpose) => purpose switch
    {
        AssetPurpose.BuildArtifact => 8L * 1024 * 1024 * 1024,
        AssetPurpose.Screenshot or AssetPurpose.MockupOutput => 50L * 1024 * 1024,
        _ => 20L * 1024 * 1024,
    };

    /// <summary>
    /// Whether a file of <paramref name="size"/> bytes is uploaded in parts.
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public static bool IsMultipart(long size) => size > MultipartThreshold;

    /// <summary>
    /// Part size of a multipart upload of <paramref name="size"/> bytes: <see cref="DefaultPartSize"/>, or larger when the
    /// file would need more than <see cref="MaxPartCount"/> parts.
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public static long PartSize(long size) => Math.Max(DefaultPartSize, (long)Math.Ceiling(size / (double)MaxPartCount));

    /// <summary>
    /// Number of parts of a multipart upload of <paramref name="size"/> bytes.
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public static int PartCount(long size) => (int)Math.Ceiling(size / (double)PartSize(size));

    /// <summary>
    /// Object key of a new file: <c>org/{organizationId}/{purpose}/{nonce}/{safe file name}</c>. The nonce is fresh per
    /// upload, so a key is never predictable or shared, and the file name keeps only safe characters.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="purpose"></param>
    /// <param name="fileName"></param>
    /// <param name="nonce"></param>
    /// <returns></returns>
    public static string ObjectKey(int organizationId, AssetPurpose purpose, string fileName, Guid nonce)
        => $"org/{organizationId}/{purpose.ToString().ToLowerInvariant()}/{nonce:N}/{SafeFileName(fileName)}";

    /// <summary>
    /// <paramref name="fileName"/> reduced to letters, digits, dot, underscore and hyphen (others become hyphens), at most
    /// 100 characters; "file" when nothing is left.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static string SafeFileName(string fileName)
    {
        var builder = new StringBuilder();

        foreach (var c in Path.GetFileName(fileName ?? string.Empty))
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-');

        var safe = builder.ToString().Trim('.', '-');

        if (safe.Length > _maxKeyFileNameLength)
            safe = safe[^_maxKeyFileNameLength..];

        return safe.Length == 0 ? "file" : safe;
    }
}
