namespace Storeasy.Application.Dtos.AccountDtos;

/// <summary>
/// One of the caller's browser push subscriptions. The full push address is not returned; its host tells the browsers
/// apart (fcm.googleapis.com, updates.push.services.mozilla.com, web.push.apple.com...).
/// </summary>
public class WebPushSubscriptionDto
{
    /// <summary>
    /// Subscription id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Host of the push service.
    /// </summary>
    public string PushServiceHost { get; set; }

    /// <summary>
    /// Browser description.
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// UTC instant of the subscription.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// UTC instant of the last delivered push.
    /// </summary>
    public DateTime? LastSuccessDate { get; set; }
}
