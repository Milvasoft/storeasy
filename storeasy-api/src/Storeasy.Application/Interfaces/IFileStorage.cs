namespace Storeasy.Application.Interfaces;

/// <summary>
/// The private S3 compatible bucket (S3, or MinIO for self-host). Clients never get bucket credentials: they upload and
/// download with short lived presigned URLs, large files (CI artifacts) with presigned multipart uploads. URL creation
/// makes no network call; the other methods call the storage and throw when it fails.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Whether a bucket is configured. The other members must not be called when false.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// A presigned PUT URL for <paramref name="key"/>. The client sends the body with the same <c>Content-Type</c>.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="contentType"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    string CreateUploadUrl(string key, string contentType, TimeSpan lifetime);

    /// <summary>
    /// A presigned GET URL for <paramref name="key"/>, optionally downloaded as <paramref name="downloadFileName"/>.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="lifetime"></param>
    /// <param name="downloadFileName"></param>
    /// <returns></returns>
    string CreateDownloadUrl(string key, TimeSpan lifetime, string downloadFileName = null);

    /// <summary>
    /// Starts a multipart upload and returns its upload id.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="contentType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> StartMultipartUploadAsync(string key, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// A presigned PUT URL for one part (from 1) of a multipart upload. The response's <c>ETag</c> header is needed to
    /// complete the upload.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="uploadId"></param>
    /// <param name="partNumber"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    string CreatePartUploadUrl(string key, string uploadId, int partNumber, TimeSpan lifetime);

    /// <summary>
    /// Completes a multipart upload from its uploaded parts.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="uploadId"></param>
    /// <param name="parts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyList<UploadedPart> parts, CancellationToken cancellationToken);

    /// <summary>
    /// Abandons a multipart upload and frees its parts.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="uploadId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken cancellationToken);

    /// <summary>
    /// Size and type of a stored object, or null when there is none.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoredObjectInfo> GetObjectInfoAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an object; a missing object is not an error.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// One uploaded part of a multipart upload.
/// </summary>
/// <param name="PartNumber">Part number, from 1.</param>
/// <param name="ETag">The <c>ETag</c> the storage answered the part upload with.</param>
public sealed record UploadedPart(int PartNumber, string ETag);

/// <summary>
/// Metadata of a stored object.
/// </summary>
/// <param name="Size">Size in bytes.</param>
/// <param name="ContentType">Stored content type.</param>
public sealed record StoredObjectInfo(long Size, string ContentType);
