using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;

namespace Storeasy.Api.Hubs;

/// <summary>
/// Live activity of organizations (docs/urun-plani.md K5), at <see cref="Path"/>. Clients authenticate with the same
/// access token as the REST API (the <c>Authorization</c> header, or the <c>access_token</c> query value for
/// websockets), then join the organizations they want to follow. A join is checked on the server against the caller's
/// membership, the same way as every organization request; events (<see cref="ActivityEventTypes"/>) arrive on the
/// client method <see cref="ActivityEventTypes.ClientMethod"/>.
/// </summary>
/// <param name="organizationAccess"></param>
[Authorize]
public class ActivityHub(IOrganizationAccess organizationAccess) : Hub
{
    /// <summary>
    /// Route of the hub.
    /// </summary>
    public const string Path = "/hubs/activity";

    private readonly IOrganizationAccess _organizationAccess = organizationAccess;

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
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(userId), Context.ConnectionAborted);

        await base.OnConnectedAsync();
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

        var role = await _organizationAccess.GetRoleAsync(organizationId, userId, Context.ConnectionAborted);

        if (role == null || !OrgRoleRules.Allows(role.Value, OrgPermission.View))
            return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(organizationId), Context.ConnectionAborted);

        return true;
    }

    /// <summary>
    /// Stops sending the organization's events to this connection.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    [HubMethodName("LeaveOrganization")]
    public Task LeaveOrganizationAsync(int organizationId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(organizationId), Context.ConnectionAborted);
}
