using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Replaces the bucket: URLs point at a fake host, and a test "uploads" by putting an object's size with
/// <see cref="Put"/>. The real S3 implementation is tested against SeaweedFS in <c>S3FileStorageTests</c>.
/// </summary>
public sealed class FakeFileStorage : IFileStorage
{
    /// <summary>
    /// Base of the fake URLs.
    /// </summary>
    public const string BaseUrl = "https://storage.test";

    private readonly ConcurrentDictionary<string, StoredObjectInfo> _objects = new();

    /// <inheritdoc/>
    public bool IsConfigured { get; set; } = true;

    /// <summary>
    /// Completed multipart uploads by key.
    /// </summary>
    public ConcurrentDictionary<string, IReadOnlyList<UploadedPart>> CompletedUploads { get; } = new();

    /// <summary>
    /// Keys deleted.
    /// </summary>
    public ConcurrentBag<string> Deleted { get; } = [];

    /// <summary>
    /// Simulates an uploaded object.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte[]> _contents = new();

    public void Put(string key, long size, string contentType = "application/octet-stream") => _objects[key] = new StoredObjectInfo(size, contentType);

    public void Put(string key, byte[] content, string contentType)
    {
        _objects[key] = new StoredObjectInfo(content.Length, contentType);
        _contents[key] = content;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
        => _contents.TryGetValue(key, out var content) ? Task.FromResult<Stream>(new MemoryStream(content, writable: false)) : throw new FileNotFoundException(key);

    /// <summary>
    /// Content of an object written with content, or null.
    /// </summary>
    public byte[] Content(string key) => _contents.TryGetValue(key, out var content) ? content : null;

    /// <inheritdoc/>
    public async Task WriteAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();

        await content.CopyToAsync(buffer, cancellationToken);

        Put(key, buffer.ToArray(), contentType);
    }

    /// <inheritdoc/>
    public string CreateUploadUrl(string key, string contentType, TimeSpan lifetime) => $"{BaseUrl}/{key}?op=put&type={Uri.EscapeDataString(contentType)}";

    /// <inheritdoc/>
    public string CreateDownloadUrl(string key, TimeSpan lifetime, string downloadFileName = null) => $"{BaseUrl}/{key}?op=get&name={Uri.EscapeDataString(downloadFileName ?? string.Empty)}";

    /// <inheritdoc/>
    public Task<string> StartMultipartUploadAsync(string key, string contentType, CancellationToken cancellationToken) => Task.FromResult($"upload-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public string CreatePartUploadUrl(string key, string uploadId, int partNumber, TimeSpan lifetime) => $"{BaseUrl}/{key}?op=part&uploadId={uploadId}&partNumber={partNumber}";

    /// <inheritdoc/>
    public Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyList<UploadedPart> parts, CancellationToken cancellationToken)
    {
        CompletedUploads[key] = parts;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<StoredObjectInfo> GetObjectInfoAsync(string key, CancellationToken cancellationToken) => Task.FromResult(_objects.TryGetValue(key, out var info) ? info : null);

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        Deleted.Add(key);

        return Task.CompletedTask;
    }
}
