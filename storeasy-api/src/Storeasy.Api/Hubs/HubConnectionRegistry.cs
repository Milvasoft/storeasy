using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Storeasy.Api.Hubs;

/// <summary>
/// The activity hub connections of this instance: the user, the session stamp the connection was opened with and the
/// organizations it joined. <see cref="SignalRRealtimeConnections"/> acts on them. Singleton.
/// </summary>
public sealed class HubConnectionRegistry
{
    private readonly ConcurrentDictionary<string, HubConnectionEntry> _connections = new(StringComparer.Ordinal);

    /// <summary>
    /// Records a new connection.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userId"></param>
    /// <param name="stamp"></param>
    /// <param name="tokenHash"></param>
    public void Add(HubCallerContext context, int userId, string stamp, string tokenHash) => _connections[context.ConnectionId] = new HubConnectionEntry(context, userId, stamp, tokenHash);

    /// <summary>
    /// Forgets a closed connection.
    /// </summary>
    /// <param name="connectionId"></param>
    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    /// <summary>
    /// Records that the connection follows an organization.
    /// </summary>
    /// <param name="connectionId"></param>
    /// <param name="organizationId"></param>
    public void Join(string connectionId, int organizationId)
    {
        if (_connections.TryGetValue(connectionId, out var entry))
            entry.Organizations[organizationId] = 0;
    }

    /// <summary>
    /// Records that the connection no longer follows an organization.
    /// </summary>
    /// <param name="connectionId"></param>
    /// <param name="organizationId"></param>
    public void Leave(string connectionId, int organizationId)
    {
        if (_connections.TryGetValue(connectionId, out var entry))
            entry.Organizations.TryRemove(organizationId, out _);
    }

    /// <summary>
    /// The connections of a user on this instance.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public List<HubConnectionEntry> OfUser(int userId) => [.. _connections.Values.Where(c => c.UserId == userId)];
}

/// <summary>
/// One live connection.
/// </summary>
/// <param name="Context">The connection (to abort it).</param>
/// <param name="UserId">Its user.</param>
/// <param name="Stamp">Session stamp of the token it was opened with.</param>
/// <param name="TokenHash">SHA-256 of the access token it was opened with.</param>
public sealed record HubConnectionEntry(HubCallerContext Context, int UserId, string Stamp, string TokenHash)
{
    /// <summary>
    /// Organizations the connection joined.
    /// </summary>
    public ConcurrentDictionary<int, byte> Organizations { get; } = new();
}
