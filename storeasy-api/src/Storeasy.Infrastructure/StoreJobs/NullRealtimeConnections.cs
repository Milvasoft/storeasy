using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.StoreJobs;

/// <summary>
/// <see cref="IRealtimeConnections"/> for a host without the live hub: nothing is connected, so nothing to do.
/// </summary>
public sealed class NullRealtimeConnections : IRealtimeConnections
{
    /// <inheritdoc/>
    public Task RemoveFromOrganizationAsync(int userId, int organizationId) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task DisconnectAsync(int userId, string currentStamp) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task DisconnectTokensAsync(int userId, IReadOnlyCollection<string> tokenHashes) => Task.CompletedTask;
}
