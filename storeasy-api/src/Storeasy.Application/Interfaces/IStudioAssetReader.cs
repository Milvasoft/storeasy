namespace Storeasy.Application.Interfaces;

/// <summary>
/// Reads the raw screenshots and background images of mockup templates with a bounded in-memory cache. Object keys are
/// never reused for other content, so a cached entry never goes stale. Registered as a singleton.
/// </summary>
public interface IStudioAssetReader
{
    /// <summary>
    /// The object's bytes. Throws when the storage fails or the object does not exist.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> ReadAsync(string objectKey, CancellationToken cancellationToken);
}
