using Microsoft.AspNetCore.SignalR;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Hubs;

/// <summary>
/// <see cref="IUserNotificationPublisher"/> over <see cref="ActivityHub"/>: each connection joins its user's own group on
/// connect, so an event sent here reaches only its recipient. Failures are logged and swallowed.
/// </summary>
/// <param name="hubContext"></param>
/// <param name="logger"></param>
public sealed class SignalRUserNotificationPublisher(IHubContext<ActivityHub> hubContext, ILogger<SignalRUserNotificationPublisher> logger) : IUserNotificationPublisher
{
    private readonly IHubContext<ActivityHub> _hubContext = hubContext;
    private readonly ILogger<SignalRUserNotificationPublisher> _logger = logger;

    /// <inheritdoc/>
    public async Task PublishAsync(IReadOnlyCollection<int> userIds, ActivityEventDto activityEvent, CancellationToken cancellationToken = default)
    {
        if (userIds == null || userIds.Count == 0)
            return;

        try
        {
            await _hubContext.Clients.Groups([.. userIds.Select(ActivityHub.UserGroupName)]).SendAsync(ActivityEventTypes.ClientMethod, activityEvent, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "User event {Type} could not be published.", activityEvent.Type);
        }
    }
}
