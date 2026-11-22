using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Chrome;

/// <summary>
/// Chrome Web Store side of a store record (<see cref="StoreApp"/> with <see cref="StoreKind.Chrome"/>, whose external id
/// is the extension id): what the last sync read with <c>fetchStatus</c>, the rating summary and the weekly users.
/// </summary>
[Table(TableNames.ChromeItems)]
[Index(nameof(StoreAppId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class ChromeItem : AuditableEntity<int>
{
    /// <summary>
    /// Length of an extension id (32 letters a to p).
    /// </summary>
    public const int ItemIdLength = 32;

    /// <summary>
    /// Maximum length of a package version (four dot separated numbers up to 65535).
    /// </summary>
    public const int VersionMaxLength = 30;

    /// <summary>
    /// Maximum length of <see cref="SyncError"/>.
    /// </summary>
    public const int SyncErrorMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Extension id.
    /// </summary>
    [MaxLength(ItemIdLength)]
    public string ItemId { get; set; }

    /// <summary>
    /// State of the published revision; <see cref="ChromeItemState.None"/> when the item is not published.
    /// </summary>
    public ChromeItemState PublishedState { get; set; }

    /// <summary>
    /// Package version of the published revision.
    /// </summary>
    [MaxLength(VersionMaxLength)]
    public string PublishedVersion { get; set; }

    /// <summary>
    /// Deploy percentage of the published revision (0 to 100).
    /// </summary>
    public int? PublishedDeployPercentage { get; set; }

    /// <summary>
    /// State of the revision submitted since the last publish; <see cref="ChromeItemState.None"/> when there is none.
    /// </summary>
    public ChromeItemState SubmittedState { get; set; }

    /// <summary>
    /// Package version of the submitted revision.
    /// </summary>
    [MaxLength(VersionMaxLength)]
    public string SubmittedVersion { get; set; }

    /// <summary>
    /// Deploy percentage of the submitted revision.
    /// </summary>
    public int? SubmittedDeployPercentage { get; set; }

    /// <summary>
    /// State of the last package upload (the store reports it for 24 hours).
    /// </summary>
    public ChromeUploadState LastUploadState { get; set; }

    /// <summary>
    /// Version of the last package storeasy uploaded.
    /// </summary>
    [MaxLength(VersionMaxLength)]
    public string UploadedVersion { get; set; }

    /// <summary>
    /// The item was warned for a policy violation and is taken down unless it is fixed.
    /// </summary>
    public bool Warned { get; set; }

    /// <summary>
    /// The item was taken down for a policy violation.
    /// </summary>
    public bool TakenDown { get; set; }

    /// <summary>
    /// Average rating shown on the store page (1 to 5); null without ratings.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    /// Number of ratings.
    /// </summary>
    public long? RatingCount { get; set; }

    /// <summary>
    /// Seven day active users on the last day the store reported.
    /// </summary>
    public long? WeeklyUsers { get; set; }

    /// <summary>
    /// Every state change of the two revisions, oldest first.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ChromeStateChange> StatusHistory { get; set; } = [];

    /// <summary>
    /// UTC instant of the last successful sync.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Why the last sync could not read the item; null after a successful one.
    /// </summary>
    [MaxLength(SyncErrorMaxLength)]
    public string SyncError { get; set; }

    /// <summary>
    /// Whether a submission waits for review or an upload is processed: the schedule reads such items every 15 minutes.
    /// Stored so the schedule can filter on it.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// The store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
