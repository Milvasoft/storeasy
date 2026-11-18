namespace Storeasy.Application.Interfaces;

/// <summary>
/// Hands web pushes to the background push sender (in memory: a push is a nudge, the in-app notification is the record).
/// Never blocks and never throws; a full queue drops the push.
/// </summary>
public interface IWebPushQueue
{
    /// <summary>
    /// Queues <paramref name="message"/> for every subscribed browser of its users. False when web push is off or the queue
    /// is full.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    bool TryEnqueue(WebPushMessage message);
}

/// <summary>
/// One push to the browsers of some users.
/// </summary>
/// <param name="UserIds">Recipients.</param>
/// <param name="Title">Notification title.</param>
/// <param name="Body">Notification text.</param>
/// <param name="Url">Web app address opened on click.</param>
/// <param name="Kind">Notification kind (lets the service worker group them).</param>
/// <param name="NotificationId">In-app notification id, when there is one.</param>
public sealed record WebPushMessage(IReadOnlyList<int> UserIds, string Title, string Body, string Url, string Kind, long? NotificationId = null);
