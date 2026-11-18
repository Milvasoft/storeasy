using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.Studio;

/// <summary>
/// <see cref="IStudioAssetReader"/> with a least recently used cache of encoded bytes bounded by
/// <paramref name="capacityBytes"/>. Previews redraw the same few screenshots on every edit; the cache keeps them off the
/// storage. A file larger than a quarter of the capacity is read but not kept.
/// </summary>
/// <param name="storage"></param>
/// <param name="capacityBytes"></param>
public sealed class StudioAssetReader(IFileStorage storage, long capacityBytes) : IStudioAssetReader
{
    private readonly IFileStorage _storage = storage;
    private readonly long _capacityBytes = capacityBytes;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, byte[] Bytes)>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, byte[] Bytes)> _order = new();

    private long _size;

    /// <summary>
    /// Bytes held by the cache.
    /// </summary>
    public long CachedBytes
    {
        get
        {
            lock (_lock)
                return _size;
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(objectKey, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);

                return node.Value.Bytes;
            }
        }

        byte[] bytes;

        await using (var stream = await _storage.OpenReadAsync(objectKey, cancellationToken))
        {
            using var buffer = new MemoryStream();

            await stream.CopyToAsync(buffer, cancellationToken);

            bytes = buffer.ToArray();
        }

        if (bytes.Length > _capacityBytes / 4)
            return bytes;

        lock (_lock)
        {
            if (_entries.ContainsKey(objectKey))
                return bytes;

            _entries[objectKey] = _order.AddFirst((objectKey, bytes));
            _size += bytes.Length;

            while (_size > _capacityBytes && _order.Last != null)
            {
                var last = _order.Last.Value;

                _order.RemoveLast();
                _entries.Remove(last.Key);
                _size -= last.Bytes.Length;
            }
        }

        return bytes;
    }
}
