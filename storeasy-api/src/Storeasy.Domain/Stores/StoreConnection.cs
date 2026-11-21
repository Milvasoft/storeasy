using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Organizations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Stores;

/// <summary>
/// An organization's credentials for one store account. The secret (the .p8 key with its ids, or the service account
/// JSON) is envelope encrypted: <see cref="EncryptedSecret"/> is sealed with a per row data key, and
/// <see cref="EncryptedDataKey"/> is that data key sealed with the master key version <see cref="KeyVersion"/>.
/// The secret never leaves the API process: no DTO carries it, only <see cref="Fingerprint"/> is shown.
/// </summary>
[Table(TableNames.StoreConnections)]
[Index(nameof(OrganizationId), nameof(Store))]
public class StoreConnection : FullAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>; the validators use the same limit.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Column length of <see cref="ReportBucket"/> (a Cloud Storage bucket name has at most 222 characters).
    /// </summary>
    public const int ReportBucketMaxLength = 222;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Which store the credentials are for.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Display name chosen by the user (e.g. "Company team key").
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Icon picked by a member, a key of <see cref="StoreConnectionIcons"/> (e.g. "appstore", "phone"). Null when none
    /// was picked; the web then shows the store's text code.
    /// </summary>
    [MaxLength(StoreConnectionIcons.MaxLength)]
    public string Icon { get; set; }

    /// <summary>
    /// Non secret identifiers safe to show (key id, masked issuer id, service account e-mail).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ConnectionFingerprint Fingerprint { get; set; }

    /// <summary>
    /// The secret sealed with the data key (nonce + ciphertext + tag).
    /// </summary>
    public byte[] EncryptedSecret { get; set; }

    /// <summary>
    /// The data key sealed with the master key (nonce + ciphertext + tag).
    /// </summary>
    public byte[] EncryptedDataKey { get; set; }

    /// <summary>
    /// Version of the master key that sealed <see cref="EncryptedDataKey"/>.
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>
    /// Result of the last verification.
    /// </summary>
    public ConnectionStatus Status { get; set; } = ConnectionStatus.NotVerified;

    /// <summary>
    /// UTC instant of the last verification. Null if never verified.
    /// </summary>
    public DateTime? LastVerifiedDate { get; set; }

    /// <summary>
    /// Short, already redacted reason of the last failed verification. Null on success.
    /// </summary>
    [MaxLength(500)]
    public string LastVerifyError { get; set; }

    /// <summary>
    /// UTC instant the secret stops working, when the store's secrets expire (Microsoft Entra client secrets, at most 24
    /// months). Entered by the user; the expiry reminders read it.
    /// </summary>
    public DateTime? SecretExpiryDate { get; set; }

    /// <summary>
    /// Google Play: the Cloud Storage bucket Play Console exports the statistics reports to (e.g.
    /// <c>pubsite_prod_rev_01234567890987654321</c>), read for the store page metrics. Null when not set. Not a secret.
    /// </summary>
    [MaxLength(ReportBucketMaxLength)]
    public string ReportBucket { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
