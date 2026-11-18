namespace Storeasy.Application.Interfaces;

/// <summary>
/// Acts on the live (SignalR) connections of a user at once, on every instance, instead of waiting for the next
/// reconnect: a removed member stops receiving the organization's events, and a rotated session stamp (password change
/// or reset, lock, deletion) closes the user's open connections. Never throws.
/// </summary>
public interface IRealtimeConnections
{
    /// <summary>
    /// Removes the user's connections from the organization's group.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    Task RemoveFromOrganizationAsync(int userId, int organizationId);

    /// <summary>
    /// Closes the user's connections opened with a session stamp other than <paramref name="currentStamp"/> (all of them
    /// when null).
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="currentStamp"></param>
    /// <returns></returns>
    Task DisconnectAsync(int userId, string currentStamp);

    /// <summary>
    /// Closes the user's connections opened with one of <paramref name="tokenHashes"/> (SHA-256 of the access tokens of
    /// signed out sessions).
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="tokenHashes"></param>
    /// <returns></returns>
    Task DisconnectTokensAsync(int userId, IReadOnlyCollection<string> tokenHashes);
}
