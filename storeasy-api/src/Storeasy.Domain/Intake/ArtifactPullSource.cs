using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Intake;

/// <summary>
/// A pull source of an app (design 13.19-13.20): a build service (EAS, Codemagic, Bitrise) calls
/// <c>/webhooks/eas/{PublicId}</c> or <c>/webhooks/artifacts/{PublicId}</c> when a build is ready, and storeasy downloads the
/// package from one of <see cref="AllowedDomains"/>. The signing secret must be read back to check signatures, so it is
/// envelope encrypted like the store connections (never in a DTO after creation).
/// </summary>
[Table(TableNames.ArtifactPullSources)]
[Index(nameof(PublicId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class ArtifactPullSource : CreationAuditableEntity<int>
{
    /// <summary>
    /// Most allowed download domains of a source.
    /// </summary>
    public const int MaxDomains = 20;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App the packages belong to.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// EAS or generic webhook.
    /// </summary>
    public ArtifactPullSourceKind Kind { get; set; }

    /// <summary>
    /// Public id in the webhook URL (<c>src_...</c>); not a secret.
    /// </summary>
    [MaxLength(40)]
    public string PublicId { get; set; }

    /// <summary>
    /// The signing secret sealed with the data key.
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
    /// Host names packages may be downloaded from (exact, or <c>*.example.com</c> for subdomains). https only.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// Platforms whose builds are taken.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ArtifactPlatform> Platforms { get; set; } = [];

    /// <summary>
    /// Whether APK builds are taken as well (EAS internal distribution builds).
    /// </summary>
    public bool IncludeApk { get; set; }

    /// <summary>
    /// UTC instant the secret was set or rotated.
    /// </summary>
    public DateTime SecretSetDate { get; set; }

    /// <summary>
    /// Member who set the secret.
    /// </summary>
    [MaxLength(256)]
    public string SecretSetByUserName { get; set; }

    /// <summary>
    /// UTC instant of the last call.
    /// </summary>
    public DateTime? LastCallDate { get; set; }

    /// <summary>
    /// Outcome of the last call.
    /// </summary>
    [MaxLength(300)]
    public string LastCallResult { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    public virtual App App { get; set; }
}
