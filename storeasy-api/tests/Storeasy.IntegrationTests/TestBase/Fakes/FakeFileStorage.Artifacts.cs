using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The intake side of the fake bucket (<see cref="IArtifactStorage"/>): parts a client "PUT" with <see cref="PutPart"/>
/// (or the server uploaded), listed for resumes and assembled into the object when the multipart upload completes; ranged
/// reads of stored objects; open multipart uploads for the cleanup.
/// </summary>
public sealed partial class FakeFileStorage : IArtifactStorage
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte[]>> _parts = new();
    private readonly ConcurrentDictionary<string, (string Key, DateTime StartDate)> _openUploads = new();

    /// <summary>
    /// Multipart uploads aborted: (key, upload id).
    /// </summary>
    public ConcurrentBag<(string Key, string UploadId)> Aborted { get; } = [];

    /// <summary>
    /// Simulates a client PUT of one part to its presigned URL; returns the ETag the storage would answer with.
    /// </summary>
    public string PutPart(string uploadId, int partNumber, byte[] content)
    {
        _parts.GetOrAdd(uploadId, _ => new ConcurrentDictionary<int, byte[]>())[partNumber] = content;

        return ETag(content);
    }

    /// <summary>
    /// Registers an open multipart upload that started at <paramref name="startDate"/> (abandoned upload tests).
    /// </summary>
    public void AddOpenUpload(string key, string uploadId, DateTime startDate) => _openUploads[uploadId] = (key, startDate);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredPart>> ListPartsAsync(string key, string uploadId, CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredPart> parts = _parts.TryGetValue(uploadId, out var stored)
            ? [.. stored.OrderBy(p => p.Key).Select(p => new StoredPart(p.Key, ETag(p.Value), p.Value.Length))]
            : [];

        return Task.FromResult(parts);
    }

    /// <inheritdoc/>
    public async Task<string> UploadPartAsync(string key, string uploadId, int partNumber, Stream content, long length, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();

        await content.CopyToAsync(buffer, cancellationToken);

        return PutPart(uploadId, partNumber, buffer.ToArray());
    }

    /// <inheritdoc/>
    public Task<byte[]> ReadRangeAsync(string key, long offset, int length, CancellationToken cancellationToken)
    {
        if (!_contents.TryGetValue(key, out var content))
            throw new FileNotFoundException(key);

        var count = (int)Math.Max(0, Math.Min(length, content.Length - offset));

        return Task.FromResult(content.AsSpan((int)offset, count).ToArray());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<OpenMultipartUpload>> ListMultipartUploadsAsync(string prefix, DateTime startedBefore, CancellationToken cancellationToken)
    {
        IReadOnlyList<OpenMultipartUpload> uploads = [.. _openUploads.Where(u => u.Value.Key.StartsWith(prefix, StringComparison.Ordinal) && u.Value.StartDate < startedBefore)
                                                                     .Select(u => new OpenMultipartUpload(u.Value.Key, u.Key, u.Value.StartDate))];

        return Task.FromResult(uploads);
    }

    private void AssembleParts(string key, string uploadId)
    {
        _openUploads.TryRemove(uploadId, out _);

        if (!_parts.TryRemove(uploadId, out var parts) || parts.IsEmpty)
            return;

        Put(key, [.. parts.OrderBy(p => p.Key).SelectMany(p => p.Value)], "application/octet-stream");
    }

    private void RecordAbort(string key, string uploadId)
    {
        Aborted.Add((key, uploadId));
        _openUploads.TryRemove(uploadId, out _);
        _parts.TryRemove(uploadId, out _);
    }

    private static string ETag(byte[] content) => $"\"{Convert.ToHexStringLower(MD5.HashData(content))}\"";
}
