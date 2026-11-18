using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Notifications;

/// <summary>
/// A user's channels for one notification kind, valid in every organization. A missing row means the kind's defaults.
/// </summary>
[Table(TableNames.NotificationPreferences)]
[Index(nameof(UserId), nameof(Kind), IsUnique = true)]
public class NotificationPreference : BaseEntity<long>
{
    /// <summary>
    /// User.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Notification kind.
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
    /// Web push (phase 8; stored, not sent yet).
    /// </summary>
    public bool WebPush { get; set; }

    /// <summary>
    /// UTC instant of the last change.
    /// </summary>
    public DateTime UpdatedDate { get; set; }
}
