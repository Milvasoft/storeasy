using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.Intake.Packages;

/// <summary>
/// A read only, seekable view of a stored object that fetches it in blocks with ranged reads, so a zip archive's central
/// directory and a few entries can be read without downloading a multi gigabyte package. Keeps the last few blocks.
/// </summary>
/// <param name="storage"></param>
/// <param name="key"></param>
/// <param name="length"></param>
public sealed class RangeReadStream(IArtifactStorage storage, string key, long length) : Stream
{
    /// <summary>
    /// Bytes per ranged read.
    /// </summary>
    public const int BlockSize = 1024 * 1024;

    private const int _cachedBlocks = 8;

    private readonly IArtifactStorage _storage = storage;
    private readonly string _key = key;
    private readonly long _length = length;
    private readonly LinkedList<(long Index, byte[] Data)> _blocks = new();

    private long _position;

    /// <summary>
    /// Number of ranged reads made (for tests and diagnostics).
    /// </summary>
    public int RangeReads { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, _length);
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var total = 0;

        while (buffer.Length > 0 && _position < _length)
        {
            var index = _position / BlockSize;
            var block = await GetBlockAsync(index, cancellationToken);
            var inBlock = (int)(_position - index * BlockSize);
            var count = Math.Min(buffer.Length, block.Length - inBlock);

            if (count <= 0)
                break;

            block.AsMemory(inBlock, count).CopyTo(buffer);
            buffer = buffer[count..];
            _position += count;
            total += count;
        }

        return total;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => _length + offset,
        };

        return _position;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private async Task<byte[]> GetBlockAsync(long index, CancellationToken cancellationToken)
    {
        var node = _blocks.First;

        while (node != null)
        {
            if (node.Value.Index == index)
            {
                _blocks.Remove(node);
                _blocks.AddFirst(node);

                return node.Value.Data;
            }

            node = node.Next;
        }

        var start = index * BlockSize;
        var size = (int)Math.Min(BlockSize, _length - start);
        var data = await _storage.ReadRangeAsync(_key, start, size, cancellationToken);

        RangeReads++;
        _blocks.AddFirst((index, data));

        if (_blocks.Count > _cachedBlocks)
            _blocks.RemoveLast();

        return data;
    }
}
