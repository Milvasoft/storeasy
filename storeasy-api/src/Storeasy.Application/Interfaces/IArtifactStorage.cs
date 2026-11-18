namespace Storeasy.Application.Interfaces;

/// <summary>
/// Bucket operations the package intake needs beyond <see cref="IFileStorage"/>: the parts of an open multipart upload
/// (resume), server side part uploads (pulled packages), ranged reads (reading a package's zip directory without
/// downloading it) and the open multipart uploads (cleanup of abandoned ones). Same bucket as <see cref="IFileStorage"/>.
/// </summary>
public interface IArtifactStorage
{
    /// <summary>
    /// Parts already uploaded to an open multipart upload, ascending; empty when the upload does not exist.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="uploadId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoredPart>> ListPartsAsync(string key, string uploadId, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads one part from the server and returns its ETag.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="uploadId"></param>
    /// <param name="partNumber"></param>
    /// <param name="content"></param>
    /// <param name="length"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> UploadPartAsync(string key, string uploadId, int partNumber, Stream content, long length, CancellationToken cancellationToken);

    /// <summary>
    /// Reads <paramref name="length"/> bytes from <paramref name="offset"/> (fewer at the end of the object).
    /// </summary>
    /// <param name="key"></param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> ReadRangeAsync(string key, long offset, int length, CancellationToken cancellationToken);

    /// <summary>
    /// Multipart uploads under <paramref name="prefix"/> started before <paramref name="startedBefore"/>.
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="startedBefore"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<OpenMultipartUpload>> ListMultipartUploadsAsync(string prefix, DateTime startedBefore, CancellationToken cancellationToken);
}

/// <summary>
/// A part in storage.
/// </summary>
/// <param name="PartNumber">Part number.</param>
/// <param name="ETag">ETag.</param>
/// <param name="Size">Bytes.</param>
public sealed record StoredPart(int PartNumber, string ETag, long Size);

/// <summary>
/// An open multipart upload.
/// </summary>
/// <param name="Key">Object key.</param>
/// <param name="UploadId">Upload id.</param>
/// <param name="StartDate">UTC start.</param>
public sealed record OpenMultipartUpload(string Key, string UploadId, DateTime StartDate);
