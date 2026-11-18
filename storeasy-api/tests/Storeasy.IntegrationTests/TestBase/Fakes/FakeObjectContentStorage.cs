using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Replaces the server side object reads and writes of the studio with an in-memory bucket. Tests put raw screenshots
/// with <see cref="Put"/> and inspect what the export wrote in <see cref="Objects"/>.
/// </summary>
public sealed class FakeObjectContentStorage : IObjectContentStorage
{
    /// <summary>
    /// Stored objects by key.
    /// </summary>
    public ConcurrentDictionary<string, byte[]> Objects { get; } = new();

    /// <summary>
    /// Keys deleted.
    /// </summary>
    public ConcurrentBag<string> Deleted { get; } = [];

    /// <inheritdoc/>
    public bool IsConfigured => true;

    /// <summary>
    /// Puts an object.
    /// </summary>
    public void Put(string key, byte[] bytes) => Objects[key] = bytes;

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken)
        => Objects.TryGetValue(key, out var bytes) ? Task.FromResult(bytes) : throw new FileNotFoundException($"No object '{key}'.");

    /// <inheritdoc/>
    public async Task WriteAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();

        await content.CopyToAsync(buffer, cancellationToken);

        Objects[key] = buffer.ToArray();
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Objects.TryRemove(key, out _);
        Deleted.Add(key);

        return Task.CompletedTask;
    }
}
