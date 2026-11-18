using Storeasy.Application.Dtos.ActivityDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Pushes live events to the members of one organization (the API sends them over the SignalR activity hub, to the
/// organization's group). Best effort: never throws and never blocks the work that produced the event.
/// </summary>
public interface IActivityPublisher
{
    /// <summary>
    /// Sends <paramref name="activityEvent"/> to the members of its organization.
    /// </summary>
    /// <param name="activityEvent"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PublishAsync(ActivityEventDto activityEvent, CancellationToken cancellationToken = default);
}
