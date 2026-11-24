using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Eas;

/// <summary>
/// The organization's Expo access token (personal or robot), one per organization. It is sealed with the same envelope
/// encryption as store connection secrets and never returned; only the Expo actor's name is shown.
/// </summary>
[Table(TableNames.ExpoConnections)]
[Index(nameof(OrganizationId), IsUnique = true)]
public class ExpoConnection : AuditableEntity<int>
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
    /// What identifies the token without the secret: the Expo actor's name (user or robot) and kind.
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
    public ConnectionStatus Status { get; set; }

    /// <summary>
    /// When Expo last accepted the token.
    /// </summary>
    public DateTime? LastVerifiedDate { get; set; }

    /// <summary>
    /// Why the last check failed (no secret).
    /// </summary>
    [MaxLength(ErrorMaxLength)]
    public string LastVerifyError { get; set; }
}
