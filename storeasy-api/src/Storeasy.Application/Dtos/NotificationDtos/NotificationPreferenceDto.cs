namespace Storeasy.Application.Dtos.NotificationDtos;

/// <summary>
/// A user's channels for one notification kind.
/// </summary>
public class NotificationPreferenceDto
{
    /// <summary>
    /// Kind.
    /// </summary>
    public NotificationType Kind { get; set; }

    /// <summary>
    /// In-app notification center.
    /// </summary>
    public bool InApp { get; set; }

    /// <summary>
    /// E-mail.
    /// </summary>
    public bool Email { get; set; }

    /// <summary>
    /// Web push (phase 8; stored, not sent).
    /// </summary>
    public bool WebPush { get; set; }

    /// <summary>
    /// Whether the kind cannot be turned off.
    /// </summary>
    public bool Locked { get; set; }
}
