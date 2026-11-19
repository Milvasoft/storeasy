using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Stores;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.StoreDtos;

/// <summary>
/// A store connection as shown to members. Carries the fingerprint only, never the secret.
/// </summary>
public class StoreConnectionDto
{
    /// <summary>
    /// Connection id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Icon key picked for the connection (<see cref="StoreConnectionIcons"/>); null shows the store's text code.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Non secret identifiers.
    /// </summary>
    public ConnectionFingerprint Fingerprint { get; set; }

    /// <summary>
    /// Result of the last verification.
    /// </summary>
    public ConnectionStatus Status { get; set; }

    /// <summary>
    /// UTC instant of the last verification.
    /// </summary>
    public DateTime? LastVerifiedDate { get; set; }

    /// <summary>
    /// Redacted reason of the last failed verification.
    /// </summary>
    public string LastVerifyError { get; set; }

    /// <summary>
    /// UTC instant the secret expires (Microsoft Entra client secrets); null when the store's secrets do not expire.
    /// </summary>
    public DateTime? CredentialExpiryDate { get; set; }

    /// <summary>
    /// UTC instant the connection was added.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Projection from a connection row. Deliberately lists every column it reads, so the encrypted columns are never
    /// loaded for a read.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<StoreConnection, StoreConnectionDto>> Projection { get; } = c => new StoreConnectionDto
    {
        Id = c.Id,
        Store = c.Store,
        Name = c.Name,
        Icon = c.Icon,
        Fingerprint = c.Fingerprint,
        Status = c.Status,
        LastVerifiedDate = c.LastVerifiedDate,
        LastVerifyError = c.LastVerifyError,
        CredentialExpiryDate = c.SecretExpiryDate,
        CreationDate = c.CreationDate,
    };
}
