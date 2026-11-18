using Storeasy.Application.Dtos.NotificationDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Delivers one notification of an organization (or of the platform) on every channel its recipients want: the in-app
/// notification center (with a live <c>notification.created</c> event), e-mail, later web push. Recipients come from the
/// organization's roles (<see cref="OrgNotification.Roles"/>) or are named; each user's preference for the kind is
/// applied (<see cref="Utils.Helpers.NotificationKinds"/> for the defaults). When the notification names an outgoing
/// webhook event, the organization's subscriptions get it too. Best effort: never throws.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Sends <paramref name="notification"/> and returns how many users got it in the notification center.
    /// </summary>
    /// <param name="notification"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> DispatchAsync(OrgNotification notification, CancellationToken cancellationToken);
}
