using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.NotificationDtos;

namespace Storeasy.Application.Features.Notifications.UpdateNotificationPreference;

/// <summary>
/// Saves the caller's channels for one notification kind. A locked kind (security and connection alerts) cannot be
/// turned off.
/// </summary>
public record UpdateNotificationPreferenceCommand : ICommand<NotificationPreferenceDto>
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
    /// Web push (phase 8).
    /// </summary>
    public bool WebPush { get; set; }
}
