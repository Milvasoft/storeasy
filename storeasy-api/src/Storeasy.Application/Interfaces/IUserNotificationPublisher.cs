using Storeasy.Application.Dtos.ActivityDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Pushes a live event to individual users (the API sends it over the activity hub to each user's own group). Used for
/// <c>notification.created</c>, which must reach only its recipients. Best effort: never throws.
/// </summary>
public interface IUserNotificationPublisher
{
    /// <summary>
    /// Sends <paramref name="activityEvent"/> to <paramref name="userIds"/>.
    /// </summary>
    /// <param name="userIds"></param>
    /// <param name="activityEvent"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PublishAsync(IReadOnlyCollection<int> userIds, ActivityEventDto activityEvent, CancellationToken cancellationToken = default);
}
