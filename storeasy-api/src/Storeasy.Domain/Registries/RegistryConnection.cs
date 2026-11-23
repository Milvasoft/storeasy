using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Registries;

/// <summary>
/// A credential of the package registries module, one per organization and kind: a GitHub token to start releases in CI or
/// a registry API key for write operations. The secret is sealed with the same envelope encryption as store connection
/// secrets and never returned; only a fingerprint is shown.
/// </summary>
[Table(TableNames.RegistryConnections)]
[Index(nameof(OrganizationId), nameof(Kind), IsUnique = true)]
public class RegistryConnection : AuditableEntity<int>
{
    /// <summary>
    /// Maximum length of <see cref="Fingerprint"/>.
    /// </summary>
    public const int FingerprintMaxLength = 200;

    /// <summary>
    /// Maximum length of <see cref="LastVerifyError"/>.
    /// </summary>
    public const int ErrorMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// What the credential is for.
    /// </summary>
    public RegistryConnectionKind Kind { get; set; }

    /// <summary>
    /// What identifies the credential without the secret: the GitHub login, or the last characters of a key.
    /// </summary>
    [MaxLength(FingerprintMaxLength)]
    public string Fingerprint { get; set; }

    /// <summary>
    /// Secret sealed with the data key.
    /// </summary>
    public byte[] EncryptedSecret { get; set; }

    /// <summary>
    /// Data key sealed with the master key.
    /// </summary>
    public byte[] EncryptedDataKey { get; set; }

    /// <summary>
    /// Master key version.
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>
    /// Result of the last check.
    /// </summary>
    public ConnectionStatus Status { get; set; } = ConnectionStatus.NotVerified;

    /// <summary>
    /// UTC instant of the last successful check.
    /// </summary>
    public DateTime? LastVerifiedDate { get; set; }

    /// <summary>
    /// Why the last check failed.
    /// </summary>
    [MaxLength(ErrorMaxLength)]
    public string LastVerifyError { get; set; }

    /// <summary>
    /// Day the credential expires, as the member gave it.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Token reminder kept for the expiry date (registry keys only).
    /// </summary>
    public int? TokenReminderId { get; set; }
}
