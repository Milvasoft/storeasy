using Microsoft.AspNetCore.SignalR;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Hubs;

/// <summary>
/// <see cref="IActivityPublisher"/> over <see cref="ActivityHub"/>: sends each event to its organization's group (through
/// the Redis backplane when several instances run). A failure (Redis down, no hub) is logged and swallowed.
/// </summary>
/// <param name="hubContext"></param>
/// <param name="logger"></param>
public sealed class SignalRActivityPublisher(IHubContext<ActivityHub> hubContext, ILogger<SignalRActivityPublisher> logger) : IActivityPublisher
{
    private readonly IHubContext<ActivityHub> _hubContext = hubContext;
    private readonly ILogger<SignalRActivityPublisher> _logger = logger;

    /// <inheritdoc/>
    public async Task PublishAsync(ActivityEventDto activityEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.Group(ActivityHub.GroupName(activityEvent.OrganizationId)).SendAsync(ActivityEventTypes.ClientMethod, activityEvent, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Activity event {Type} of organization {OrganizationId} could not be published.", activityEvent.Type, activityEvent.OrganizationId);
        }
    }
}
