using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// <see cref="IUserNotificationPublisher"/> that drops the events (a host without SignalR).
/// </summary>
public sealed class NullUserNotificationPublisher : IUserNotificationPublisher
{
    /// <inheritdoc/>
    public Task PublishAsync(IReadOnlyCollection<int> userIds, ActivityEventDto activityEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
