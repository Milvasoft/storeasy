using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain;

/// <summary>
/// A browser's web push subscription (Push API) of a user: the push service address and the browser's keys the payload is
/// encrypted to (RFC 8291). One row per address; a browser that subscribes again for another user moves to that user. A
/// subscription the push service reports gone (404, 410) is deleted.
/// </summary>
[Table(TableNames.WebPushSubscriptions)]
[Index(nameof(Endpoint), IsUnique = true)]
[Index(nameof(UserId))]
public class WebPushSubscription : CreationAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Endpoint"/>.
    /// </summary>
    public const int EndpointMaxLength = 1000;

    /// <summary>
    /// How many subscriptions (browsers) one user may have; the oldest is dropped beyond it.
    /// </summary>
    public const int MaxPerUser = 20;

    /// <summary>
    /// The subscribed user.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Push service address (https).
    /// </summary>
    [MaxLength(EndpointMaxLength)]
    public string Endpoint { get; set; }

    /// <summary>
    /// Browser's P-256 public key, base64url (<c>keys.p256dh</c>).
    /// </summary>
    [MaxLength(200)]
    public string P256dh { get; set; }

    /// <summary>
    /// Browser's authentication secret, base64url (<c>keys.auth</c>).
    /// </summary>
    [MaxLength(100)]
    public string Auth { get; set; }

    /// <summary>
    /// Browser description for the settings list.
    /// </summary>
    [MaxLength(300)]
    public string UserAgent { get; set; }

    /// <summary>
    /// UTC instant of the last delivered push.
    /// </summary>
    public DateTime? LastSuccessDate { get; set; }

    /// <summary>
    /// Navigation property of the user.
    /// </summary>
    public virtual User User { get; set; }
}
