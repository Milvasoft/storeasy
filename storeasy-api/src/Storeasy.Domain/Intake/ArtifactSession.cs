using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Intake;

/// <summary>
/// One package received for an app (design 13.13-13.17, tools/CONTRACT.md 3): the presigned multipart upload into the
/// bucket, then parsing, the catalog checks, the delivery rule and the delivery to the store, each recorded in
/// <see cref="Steps"/>. The result for the CI is <see cref="CiExitCode"/> and <see cref="CiSummary"/>. The same SHA-256 of
/// the same app is processed once: a later session becomes <see cref="ArtifactSessionStatus.Duplicate"/>.
/// </summary>
[Table(TableNames.ArtifactSessions)]
[Index(nameof(OrganizationId), nameof(AppId), nameof(Sha256))]
[Index(nameof(OrganizationId), nameof(Status))]
[Index(nameof(Status), nameof(StartDate))]
public class ArtifactSession : CreationAuditableEntity<long>
{
    /// <summary>
    /// Column length of <see cref="FileName"/>.
    /// </summary>
    public const int FileNameMaxLength = 255;

    /// <summary>
    /// Column length of <see cref="StatusDescription"/>, <see cref="CiSummary"/> and <see cref="HoldReason"/>.
    /// </summary>
    public const int TextMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App the package was sent to.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Platform from the file extension (and confirmed by parsing).
    /// </summary>
    public ArtifactPlatform Platform { get; set; }

    /// <summary>
    /// File name as sent.
    /// </summary>
    [MaxLength(FileNameMaxLength)]
    public string FileName { get; set; }

    /// <summary>
    /// Declared size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Declared lower case hex SHA-256 of the whole file.
    /// </summary>
    [MaxLength(64)]
    public string Sha256 { get; set; }

    /// <summary>
    /// MIME type as sent.
    /// </summary>
    [MaxLength(100)]
    public string ContentType { get; set; }

    /// <summary>
    /// Where the package came from.
    /// </summary>
    public ArtifactSource Source { get; set; }

    /// <summary>
    /// CI details of the source (run, repository, branch, commit).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ArtifactSourceRef SourceRef { get; set; }

    /// <summary>
    /// Target given for this session only, overriding the delivery rule.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ArtifactDeliveryOverride Delivery { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ArtifactSessionStatus Status { get; set; } = ArtifactSessionStatus.Uploading;

    /// <summary>
    /// One line, human readable state.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string StatusDescription { get; set; }

    /// <summary>
    /// UTC instant the session was opened.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// UTC instant the session became final.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Store job that processes the package.
    /// </summary>
    public long? StoreJobId { get; set; }

    /// <summary>
    /// The earlier session with the same file (for <see cref="ArtifactSessionStatus.Duplicate"/>).
    /// </summary>
    public long? OriginalSessionId { get; set; }

    /// <summary>
    /// Object key of the package in the bucket.
    /// </summary>
    [MaxLength(512)]
    public string ObjectKey { get; set; }

    /// <summary>
    /// Multipart upload id while uploading.
    /// </summary>
    [MaxLength(512)]
    public string UploadId { get; set; }

    /// <summary>
    /// Part size of the multipart upload (constant for the session).
    /// </summary>
    public long PartSize { get; set; }

    /// <summary>
    /// How often the client resumed the upload.
    /// </summary>
    public int ResumeCount { get; set; }

    /// <summary>
    /// Whether the stored file was deleted (retention, member, rejection).
    /// </summary>
    public bool PackageDeleted { get; set; }

    /// <summary>
    /// Processing steps.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ArtifactStep> Steps { get; set; } = [];

    /// <summary>
    /// What was read from the package.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ArtifactPackageInfo Package { get; set; }

    /// <summary>
    /// Results of the catalog checks.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ArtifactCheck> Checks { get; set; } = [];

    /// <summary>
    /// The delivery target that was applied.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ArtifactAppliedRule AppliedRule { get; set; }

    /// <summary>
    /// Store record the package belongs to (matched by parsing).
    /// </summary>
    public int? StoreAppId { get; set; }

    /// <summary>
    /// How the upload was authenticated.
    /// </summary>
    public IdentityKind? IdentityKind { get; set; }

    /// <summary>
    /// Trusted publisher rule of an OIDC upload.
    /// </summary>
    public int? TrustedPublisherId { get; set; }

    /// <summary>
    /// CI token of the upload.
    /// </summary>
    public int? CiTokenId { get; set; }

    /// <summary>
    /// Prefix of that CI token.
    /// </summary>
    [MaxLength(7)]
    public string CiTokenPrefix { get; set; }

    /// <summary>
    /// Member who uploaded (web, personal access token).
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Name of that member.
    /// </summary>
    [MaxLength(256)]
    public string UserName { get; set; }

    /// <summary>
    /// Why delivery waits for a member (<see cref="ArtifactSessionStatus.Held"/>).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string HoldReason { get; set; }

    /// <summary>
    /// Exit code for the CI once final (0 delivered, duplicate or held; 2 rejected; 3 identity; 4 store error).
    /// </summary>
    public int? CiExitCode { get; set; }

    /// <summary>
    /// One line result for the CI.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string CiSummary { get; set; }

    /// <summary>
    /// Store's id of the delivered build (App Store Connect build id, Play version code, Microsoft Store draft submission id), when known.
    /// </summary>
    [MaxLength(200)]
    public string StoreBuildId { get; set; }

    /// <summary>
    /// Store upload of the package (App Store Connect build upload id), while the store processes it.
    /// </summary>
    [MaxLength(200)]
    public string StoreUploadId { get; set; }

    /// <summary>
    /// Pull source of a pulled package (EAS, generic webhook).
    /// </summary>
    public int? PullSourceId { get; set; }

    /// <summary>
    /// Address a pulled package is downloaded from (checked against the source's allowed domains); cleared after the
    /// download. Never shown, it may carry a signed query.
    /// </summary>
    [MaxLength(2000)]
    public string DownloadUrl { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    public virtual App App { get; set; }
}
