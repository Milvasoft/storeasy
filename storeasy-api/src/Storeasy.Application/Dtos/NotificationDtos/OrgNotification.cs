namespace Storeasy.Application.Dtos.NotificationDtos;

/// <summary>
/// A notification to dispatch (see <see cref="INotificationDispatcher"/>). The text is a message key with arguments: it
/// is localized for the notification center and the e-mail, and kept in <c>Data</c> so the web app can localize it
/// again in the reader's language.
/// </summary>
public class OrgNotification
{
    /// <summary>
    /// Organization; null for platform notifications (then only <see cref="UserIds"/> or <see cref="PlatformAdmins"/>).
    /// </summary>
    public int? OrganizationId { get; set; }

    /// <summary>
    /// Kind (decides the preference applied).
    /// </summary>
    public NotificationType Kind { get; set; }

    /// <summary>
    /// Members with one of these roles get it (see <see cref="Utils.Helpers.NotificationAudiences"/>).
    /// </summary>
    public List<OrgRole> Roles { get; set; } = [];

    /// <summary>
    /// Users named directly (kept only when they are members of <see cref="OrganizationId"/>).
    /// </summary>
    public List<int> UserIds { get; set; } = [];

    /// <summary>
    /// Sends to the platform admins (Manager users).
    /// </summary>
    public bool PlatformAdmins { get; set; }

    /// <summary>
    /// Message key of the text.
    /// </summary>
    public string TextKey { get; set; }

    /// <summary>
    /// Arguments of <see cref="TextKey"/>.
    /// </summary>
    public object[] TextArgs { get; set; } = [];

    /// <summary>
    /// Extra data for the web app (ids, states).
    /// </summary>
    public object Data { get; set; }

    /// <summary>
    /// Web app route to open (e.g. <c>/apps/12/rejections/5</c>).
    /// </summary>
    public string ActionLink { get; set; }

    /// <summary>
    /// Related record kind.
    /// </summary>
    public NotificationEntity RelatedEntity { get; set; }

    /// <summary>
    /// Related record id.
    /// </summary>
    public string RelatedEntityId { get; set; }

    /// <summary>
    /// storeasy app, when the notification is about one.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Outgoing webhook event to queue as well (<see cref="Utils.Constants.OrgWebhookEventTypes"/>); null for none.
    /// </summary>
    public string WebhookEventType { get; set; }

    /// <summary>
    /// Data of the outgoing webhook event.
    /// </summary>
    public object WebhookData { get; set; }

    /// <summary>
    /// Live event type to publish to the organization's members as well; null for none.
    /// </summary>
    public string ActivityType { get; set; }
}
