using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.StoreJobs;

/// <summary>
/// <see cref="IActivityPublisher"/> of a host without live clients: drops every event. The API replaces it with its
/// SignalR publisher.
/// </summary>
public sealed class NullActivityPublisher : IActivityPublisher
{
    /// <inheritdoc/>
    public Task PublishAsync(ActivityEventDto activityEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
