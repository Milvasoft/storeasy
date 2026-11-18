using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Notifications;

/// <summary>
/// An outgoing webhook subscription of an organization: storeasy posts the subscribed events to <see cref="Url"/>, signed
/// with HMAC-SHA256 of the secret (envelope encrypted at rest).
/// </summary>
[Table(TableNames.OrganizationWebhooks)]
[Index(nameof(OrganizationId))]
public class OrganizationWebhook : FullAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Url"/>.
    /// </summary>
    public const int UrlMaxLength = 1000;

    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// HTTPS address the events are posted to.
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string Url { get; set; }

    /// <summary>
    /// Subscribed event types; empty means every event.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> Events { get; set; } = [];

    /// <summary>
    /// Whether deliveries are made.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The secret sealed with the data key.
    /// </summary>
    public byte[] EncryptedSecret { get; set; }

    /// <summary>
    /// The data key sealed with the master key.
    /// </summary>
    public byte[] EncryptedDataKey { get; set; }

    /// <summary>
    /// Master key version of <see cref="EncryptedDataKey"/>.
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>
    /// UTC instant of the last delivery attempt.
    /// </summary>
    public DateTime? LastDeliveryDate { get; set; }

    /// <summary>
    /// Result of the last delivery attempt.
    /// </summary>
    public WebhookDeliveryStatus? LastDeliveryStatus { get; set; }
}
