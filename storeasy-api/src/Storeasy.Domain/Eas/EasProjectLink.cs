using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Eas;

/// <summary>
/// The EAS project of a storeasy app (at most one per app). Kept apart from the app, which holds nothing store or service
/// specific.
/// </summary>
[Table(TableNames.EasProjectLinks)]
[Index(nameof(AppId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class EasProjectLink : AuditableEntity<int>
{
    /// <summary>
    /// Maximum length of the short text columns.
    /// </summary>
    public const int ShortMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Expo project (app) id.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string ProjectId { get; set; }

    /// <summary>
    /// Expo account that owns the project.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string AccountName { get; set; }

    /// <summary>
    /// Project slug.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string Slug { get; set; }

    /// <summary>
    /// GitHub repository (owner/repo) linked to the project on expo.dev; null without the GitHub integration.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string GitHubRepository { get; set; }

    /// <summary>
    /// Default branch of that repository.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string DefaultBranch { get; set; }

    /// <summary>
    /// When the builds were last read from Expo.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Why the last read failed.
    /// </summary>
    [MaxLength(500)]
    public string SyncError { get; set; }

    /// <summary>
    /// Default of what happens after a build of this app finished.
    /// </summary>
    public EasAfterBuild AfterBuild { get; set; } = EasAfterBuild.None;

    /// <summary>
    /// Default: once the build reached the test channel, prepare (never approve) the review or production plan.
    /// </summary>
    public bool PrepareReviewPlan { get; set; }

    /// <summary>
    /// Default Play track of EAS Submit (Android).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string SubmitTrack { get; set; }

    /// <summary>
    /// Default Play release status of EAS Submit (Android).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string SubmitReleaseStatus { get; set; }
}
