using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Stores;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Events;

/// <summary>
/// An inbound webhook address of an organization (docs/urun-plani.md 2.5). The address carries <see cref="PublicId"/>,
/// never the database id; the HMAC secret is envelope encrypted like store secrets. After a rotation the previous secret
/// stays valid until <see cref="PreviousSecretValidUntil"/>, so events signed during the switch are not lost.
/// </summary>
[Table(TableNames.WebhookEndpoints)]
[Index(nameof(PublicId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class WebhookEndpoint : FullAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="PublicId"/>.
    /// </summary>
    public const int PublicIdMaxLength = 64;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store connection the endpoint belongs to (App Store Connect endpoints); null for a signed endpoint.
    /// </summary>
    public int? StoreConnectionId { get; set; }

    /// <summary>
    /// Sender and signature scheme.
    /// </summary>
    public WebhookSource Source { get; set; }

    /// <summary>
    /// Random id put into the address.
    /// </summary>
    [MaxLength(PublicIdMaxLength)]
    public string PublicId { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(100)]
    public string Name { get; set; }

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
    /// The secret before the last rotation, sealed.
    /// </summary>
    public byte[] PreviousEncryptedSecret { get; set; }

    /// <summary>
    /// Data key of <see cref="PreviousEncryptedSecret"/>, sealed.
    /// </summary>
    public byte[] PreviousEncryptedDataKey { get; set; }

    /// <summary>
    /// Master key version of <see cref="PreviousEncryptedDataKey"/>.
    /// </summary>
    public int? PreviousKeyVersion { get; set; }

    /// <summary>
    /// UTC instant until which the previous secret is still accepted.
    /// </summary>
    public DateTime? PreviousSecretValidUntil { get; set; }

    /// <summary>
    /// UTC instant the current secret was set.
    /// </summary>
    public DateTime SecretSetDate { get; set; }

    /// <summary>
    /// Whether the sender knows the address.
    /// </summary>
    public WebhookRegistrationState Registration { get; set; } = WebhookRegistrationState.Pending;

    /// <summary>
    /// Redacted reason of a failed registration.
    /// </summary>
    [MaxLength(500)]
    public string RegistrationError { get; set; }

    /// <summary>
    /// Webhooks the store keeps for this endpoint (one per App Store Connect app).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ExternalWebhookRegistration> ExternalWebhooks { get; set; } = [];

    /// <summary>
    /// Event types the endpoint is registered for.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> Events { get; set; } = [];

    /// <summary>
    /// UTC instant of the last event with a valid signature.
    /// </summary>
    public DateTime? LastEventDate { get; set; }

    /// <summary>
    /// Navigation property of the connection.
    /// </summary>
    [ForeignKey(nameof(StoreConnectionId))]
    public virtual StoreConnection StoreConnection { get; set; }
}
