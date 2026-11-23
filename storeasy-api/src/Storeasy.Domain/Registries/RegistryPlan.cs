using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Registries;

/// <summary>
/// A planned registry write (a release started in CI, or an operation such as unlist, deprecate, dist-tag or yank). It is
/// shown with its exact changes, approved with a typed confirmation by a member with the permission it needs, and only
/// then written by a job. Nothing is ever started automatically.
/// </summary>
[Table(TableNames.RegistryPlans)]
[Index(nameof(OrganizationId), nameof(Status))]
[Index(nameof(FollowStatus))]
public class RegistryPlan : AuditableEntity<long>
{
    /// <summary>
    /// Maximum length of <see cref="Title"/>.
    /// </summary>
    public const int TitleMaxLength = 300;

    /// <summary>
    /// Maximum length of short texts (version, tag, sha, confirmation).
    /// </summary>
    public const int ShortMaxLength = 200;

    /// <summary>
    /// Maximum length of <see cref="Error"/>.
    /// </summary>
    public const int ErrorMaxLength = 1000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The package, for a package plan.
    /// </summary>
    public int? TrackedPackageId { get; set; }

    /// <summary>
    /// The group, for a group release.
    /// </summary>
    public int? PackageGroupId { get; set; }

    /// <summary>
    /// What the plan does.
    /// </summary>
    public RegistryPlanKind Kind { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ChangePlanStatus Status { get; set; } = ChangePlanStatus.Pending;

    /// <summary>
    /// One line summary.
    /// </summary>
    [MaxLength(TitleMaxLength)]
    public string Title { get; set; }

    /// <summary>
    /// Version the plan releases or acts on.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string Version { get; set; }

    /// <summary>
    /// Version before the release (the package's latest, or the group's highest).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string CurrentVersion { get; set; }

    /// <summary>
    /// Tag the release pushes.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string Tag { get; set; }

    /// <summary>
    /// Text the approver types.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string ConfirmText { get; set; }

    /// <summary>
    /// Permission the approver needs.
    /// </summary>
    public OrgPermission RequiredPermission { get; set; }

    /// <summary>
    /// The release settings the plan was made with.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ReleaseSettings Release { get; set; }

    /// <summary>
    /// File changes of a version bump.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ReleaseFileChange> Files { get; set; } = [];

    /// <summary>
    /// Packages the plan covers (one for a package plan, the members for a group release).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<int> PackageIds { get; set; } = [];

    /// <summary>
    /// Inputs of a workflow dispatch.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public Dictionary<string, string> DispatchInputs { get; set; } = [];

    /// <summary>
    /// Branch head the plan read; a tag is pushed on it, and approval refuses when the branch moved for a bump.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string BaseSha { get; set; }

    /// <summary>
    /// Text argument of an operation (deprecation message, dist-tag name).
    /// </summary>
    [MaxLength(TrackedPackage.MessageMaxLength)]
    public string Argument { get; set; }

    /// <summary>
    /// UTC instant after which the plan can no longer be approved.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Who planned it.
    /// </summary>
    [MaxLength(100)]
    public string CreatedByUserName { get; set; }

    /// <summary>
    /// Who decided it.
    /// </summary>
    [MaxLength(100)]
    public string DecidedByUserName { get; set; }

    /// <summary>
    /// UTC instant of the decision.
    /// </summary>
    public DateTime? DecisionDate { get; set; }

    /// <summary>
    /// The job that writes it.
    /// </summary>
    public long? StoreJobId { get; set; }

    /// <summary>
    /// Commit of the version bump.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string CommitSha { get; set; }

    /// <summary>
    /// CI run storeasy follows.
    /// </summary>
    public long? RunId { get; set; }

    /// <summary>
    /// Address of the CI run.
    /// </summary>
    [MaxLength(TrackedPackage.UrlMaxLength)]
    public string RunUrl { get; set; }

    /// <summary>
    /// Status of the CI run (<c>queued</c>, <c>in_progress</c>, <c>completed</c>).
    /// </summary>
    [MaxLength(50)]
    public string RunStatus { get; set; }

    /// <summary>
    /// Conclusion of the completed CI run (<c>success</c>, <c>failure</c>, ...).
    /// </summary>
    [MaxLength(50)]
    public string RunConclusion { get; set; }

    /// <summary>
    /// Where a started release stands; null for operations and plans not applied.
    /// </summary>
    public ReleaseFollowStatus? FollowStatus { get; set; }

    /// <summary>
    /// UTC instant the release was started.
    /// </summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>
    /// UTC instant the follow ended.
    /// </summary>
    public DateTime? FinishedDate { get; set; }

    /// <summary>
    /// Why writing or following failed.
    /// </summary>
    [MaxLength(ErrorMaxLength)]
    public string Error { get; set; }
}
