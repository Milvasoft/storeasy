namespace Storeasy.Application.Interfaces;

/// <summary>
/// Server side reads and writes of object contents in the private bucket, for work the API does itself (the studio reads
/// raw screenshots and writes rendered images). Clients still move files only through presigned URLs
/// (<see cref="IFileStorage"/>). Calls the storage and throws when it fails.
/// </summary>
public interface IObjectContentStorage
{
    /// <summary>
    /// Whether a bucket is configured.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Reads a whole object. Throws when there is no such object.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Writes an object (replacing one with the same key).
    /// </summary>
    /// <param name="key"></param>
    /// <param name="content">Content, read from its current position to the end.</param>
    /// <param name="contentType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task WriteAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an object; a missing object is not an error.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
