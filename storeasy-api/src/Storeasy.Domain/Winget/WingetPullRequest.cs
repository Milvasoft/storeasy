using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Winget;

/// <summary>
/// A pull request to winget-pkgs for one package: opened by storeasy from an approved change plan, or found by the sync
/// among the fork owner's pull requests. Its state is the winget review state (<see cref="WingetPullRequestState"/>).
/// </summary>
[Table(TableNames.WingetPullRequests)]
[Index(nameof(StoreAppId), nameof(Number), IsUnique = true)]
[Index(nameof(OrganizationId), nameof(IsOpen))]
public class WingetPullRequest : AuditableEntity<long>
{
    /// <summary>
    /// Maximum length of <see cref="Title"/>.
    /// </summary>
    public const int TitleMaxLength = 256;

    /// <summary>
    /// Maximum length of <see cref="HeadBranch"/>.
    /// </summary>
    public const int BranchMaxLength = 255;

    /// <summary>
    /// Most comments kept in <see cref="Comments"/> (the newest).
    /// </summary>
    public const int MaxComments = 10;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The store record of the package.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Pull request number in winget-pkgs.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Page of the pull request.
    /// </summary>
    [MaxLength(500)]
    public string Url { get; set; }

    /// <summary>
    /// Title.
    /// </summary>
    [MaxLength(TitleMaxLength)]
    public string Title { get; set; }

    /// <summary>
    /// Branch of the fork the pull request comes from.
    /// </summary>
    [MaxLength(BranchMaxLength)]
    public string HeadBranch { get; set; }

    /// <summary>
    /// Head commit; check runs are read on it.
    /// </summary>
    [MaxLength(64)]
    public string HeadSha { get; set; }

    /// <summary>
    /// Package version the manifests are for.
    /// </summary>
    [MaxLength(WingetPackage.VersionMaxLength)]
    public string PackageVersion { get; set; }

    /// <summary>
    /// What the pull request changes; null for one found by the sync.
    /// </summary>
    public WingetChangeKind? ChangeKind { get; set; }

    /// <summary>
    /// Review state.
    /// </summary>
    public WingetPullRequestState State { get; set; }

    /// <summary>
    /// Whether the pull request is open (the schedule syncs only open ones).
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Labels, as GitHub shows them.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> Labels { get; set; } = [];

    /// <summary>
    /// Check runs of the head commit.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<WingetCheck> Checks { get; set; } = [];

    /// <summary>
    /// Newest bot and moderator comments (at most <see cref="MaxComments"/>).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<WingetComment> Comments { get; set; } = [];

    /// <summary>
    /// States seen, oldest first.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<WingetStateChange> StatusHistory { get; set; } = [];

    /// <summary>
    /// Whether storeasy opened the pull request.
    /// </summary>
    public bool CreatedByStoreasy { get; set; }

    /// <summary>
    /// The change plan that opened it, when storeasy did.
    /// </summary>
    public long? PlanId { get; set; }

    /// <summary>
    /// UTC instant the pull request was opened.
    /// </summary>
    public DateTime? OpenedDate { get; set; }

    /// <summary>
    /// UTC instant it was merged.
    /// </summary>
    public DateTime? MergedDate { get; set; }

    /// <summary>
    /// UTC instant it was closed (merged or not).
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>
    /// UTC instant of the last sync.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// The store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
