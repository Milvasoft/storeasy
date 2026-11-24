using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Eas;

/// <summary>
/// An EAS build of a linked project: one storeasy started from GitHub (first only a background job receipt, then the build)
/// or one started elsewhere (eas-cli, a GitHub trigger) and read from Expo.
/// </summary>
[Table(TableNames.EasBuilds)]
[Index(nameof(OrganizationId), nameof(BuildId), IsUnique = true)]
[Index(nameof(AppId), nameof(CreationDate))]
[Index(nameof(Status))]
public class EasBuild : AuditableEntity<long>
{
    /// <summary>
    /// Maximum length of the short text columns.
    /// </summary>
    public const int ShortMaxLength = 200;

    /// <summary>
    /// Maximum length of addresses.
    /// </summary>
    public const int UrlMaxLength = 2000;

    /// <summary>
    /// Maximum length of <see cref="ErrorMessage"/>.
    /// </summary>
    public const int ErrorMaxLength = 1000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Expo project id.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string ProjectId { get; set; }

    /// <summary>
    /// Expo build id; null until Expo created the build.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string BuildId { get; set; }

    /// <summary>
    /// Expo background job receipt of a build storeasy requested from GitHub.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string ReceiptId { get; set; }

    /// <summary>
    /// Platform.
    /// </summary>
    public EasPlatform Platform { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public EasBuildStatus Status { get; set; }

    /// <summary>
    /// Build profile of eas.json.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string Profile { get; set; }

    /// <summary>
    /// Git ref storeasy asked for (branch, tag or commit).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string GitRef { get; set; }

    /// <summary>
    /// Commit the build was made from.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string GitCommitHash { get; set; }

    /// <summary>
    /// App version (version / versionName).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string AppVersion { get; set; }

    /// <summary>
    /// Build number (buildNumber / versionCode).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string AppBuildVersion { get; set; }

    /// <summary>
    /// Distribution (store, internal, simulator).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string Distribution { get; set; }

    /// <summary>
    /// Who started it, as Expo names the actor; or the storeasy member.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string StartedBy { get; set; }

    /// <summary>
    /// Whether storeasy started it.
    /// </summary>
    public bool StartedInStoreasy { get; set; }

    /// <summary>
    /// The build's page on expo.dev (logs and artifacts).
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string PageUrl { get; set; }

    /// <summary>
    /// The first build log file, as Expo returns it (signed and short lived).
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string LogsUrl { get; set; }

    /// <summary>
    /// The built package (IPA, AAB, APK).
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string ArtifactUrl { get; set; }

    /// <summary>
    /// When Expo created the build (or storeasy asked for it).
    /// </summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>
    /// When it finished, errored or was canceled.
    /// </summary>
    public DateTime? FinishedDate { get; set; }

    /// <summary>
    /// Expo's error code.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string ErrorCode { get; set; }

    /// <summary>
    /// Expo's error message.
    /// </summary>
    [MaxLength(ErrorMaxLength)]
    public string ErrorMessage { get; set; }

    /// <summary>
    /// The intake session the artifact was sent to.
    /// </summary>
    public long? ArtifactSessionId { get; set; }

    /// <summary>
    /// Whether the end (finished, errored, canceled) was reported.
    /// </summary>
    public bool EndReported { get; set; }

    /// <summary>
    /// What happens once the build finished (chosen when it was started).
    /// </summary>
    public EasAfterBuild AfterBuild { get; set; } = EasAfterBuild.None;

    /// <summary>
    /// Once the build reached the test channel, prepare (never approve) the review or production plan.
    /// </summary>
    public bool PrepareReviewPlan { get; set; }

    /// <summary>
    /// Play track of the chained EAS Submit (Android).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string SubmitTrack { get; set; }

    /// <summary>
    /// Play release status of the chained EAS Submit (Android).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string SubmitReleaseStatus { get; set; }

    /// <summary>
    /// Whether the after build step ran.
    /// </summary>
    public bool ChainDone { get; set; }

    /// <summary>
    /// Whether the review plan step ended (plans made, or given up).
    /// </summary>
    public bool ReviewPlanDone { get; set; }

    /// <summary>
    /// The change plans prepared for review or production (pending until a member approves them).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<long> ReviewPlanIds { get; set; } = [];

    /// <summary>
    /// Why a chained step could not run.
    /// </summary>
    [MaxLength(ErrorMaxLength)]
    public string ChainError { get; set; }
}
