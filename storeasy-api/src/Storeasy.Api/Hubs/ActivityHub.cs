using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Storeasy.Application.Interfaces;
using Storeasy.Api.Utils;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;

namespace Storeasy.Api.Hubs;

/// <summary>
/// Live activity of organizations (docs/urun-plani.md K5), at <see cref="Path"/>. Clients authenticate with the same
/// access token as the REST API (the <c>Authorization</c> header, or the <c>access_token</c> query value for
/// websockets), then join the organizations they want to follow. A join is checked on the server against the caller's
/// membership, the same way as every organization request; events (<see cref="ActivityEventTypes"/>) arrive on the
/// client method <see cref="ActivityEventTypes.ClientMethod"/>. Each connection is recorded with its session stamp and the
/// organizations it joined (<see cref="HubConnectionRegistry"/>), so a removed member leaves the organization's group and
/// a rotated stamp closes the connection right away (<see cref="SignalRRealtimeConnections"/>).
/// </summary>
/// <param name="organizationAccess"></param>
/// <param name="registry"></param>
[Authorize]
public class ActivityHub(IOrganizationAccess organizationAccess, HubConnectionRegistry registry) : Hub
{
    /// <summary>
    /// Route of the hub.
    /// </summary>
    public const string Path = "/hubs/activity";

    private readonly IOrganizationAccess _organizationAccess = organizationAccess;
    private readonly HubConnectionRegistry _registry = registry;

    /// <summary>
    /// Group of the connections that follow <paramref name="organizationId"/>.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    public static string GroupName(int organizationId) => $"org:{organizationId}";

    /// <summary>
    /// Group of the connections of user <paramref name="userId"/> (events only that user may see, like
    /// <c>notification.created</c>).
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public static string UserGroupName(int userId) => $"user:{userId}";

    /// <summary>
    /// Joins the connection to its user's own group.
    /// </summary>
    /// <returns></returns>
    public override async Task OnConnectedAsync()
    {
        if (int.TryParse(Context.User?.FindFirst(GlobalConstant.UserIdClaimName)?.Value, out var userId))
        {
            _registry.Add(Context, userId, Context.User?.FindFirst(GlobalConstant.SessionStampClaimName)?.Value, AccessTokenHash());

            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(userId), Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// SHA-256 of the access token the connection was opened with (header or, for websockets, the query string).
    /// </summary>
    private string AccessTokenHash()
    {
        var token = BearerTokens.Read(Context.GetHttpContext()?.Request);

        return string.IsNullOrEmpty(token) ? null : SecureTokens.Hash(token);
    }

    /// <summary>
    /// Forgets the connection.
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public override Task OnDisconnectedAsync(Exception exception)
    {
        _registry.Remove(Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Starts sending the organization's events to this connection. Returns false (and joins nothing) when the caller is
    /// not a member, so organization ids cannot be probed.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    [HubMethodName("JoinOrganization")]
    public async Task<bool> JoinOrganizationAsync(int organizationId)
    {
        if (!int.TryParse(Context.User?.FindFirst(GlobalConstant.UserIdClaimName)?.Value, out var userId))
            return false;

        var access = await _organizationAccess.GetAccessAsync(organizationId, userId, Context.ConnectionAborted);

        if (access?.Allows(OrgPermission.View) != true)
            return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(organizationId), Context.ConnectionAborted);

        _registry.Join(Context.ConnectionId, organizationId);

        return true;
    }

    /// <summary>
    /// Stops sending the organization's events to this connection.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    [HubMethodName("LeaveOrganization")]
    public Task LeaveOrganizationAsync(int organizationId)
    {
        _registry.Leave(Context.ConnectionId, organizationId);

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(organizationId), Context.ConnectionAborted);
    }
}
