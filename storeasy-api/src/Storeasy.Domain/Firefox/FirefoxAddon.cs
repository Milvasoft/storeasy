using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Firefox;

/// <summary>
/// addons.mozilla.org side of a store record (<see cref="StoreApp"/> with <see cref="StoreKind.Firefox"/>, whose external id
/// is the numeric AMO add-on id and whose bundle id is the add-on guid): what the last sync read from the AMO API v5.
/// </summary>
[Table(TableNames.FirefoxAddons)]
[Index(nameof(StoreAppId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class FirefoxAddon : AuditableEntity<int>
{
    /// <summary>
    /// Maximum length of the guid (an e-mail like id or a braced UUID).
    /// </summary>
    public const int GuidMaxLength = 255;

    /// <summary>
    /// Maximum length of the slug.
    /// </summary>
    public const int SlugMaxLength = 255;

    /// <summary>
    /// Maximum length of a version string.
    /// </summary>
    public const int VersionMaxLength = 100;

    /// <summary>
    /// Maximum length of a locale code.
    /// </summary>
    public const int LocaleMaxLength = 20;

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
    /// Numeric AMO add-on id.
    /// </summary>
    public long AddonId { get; set; }

    /// <summary>
    /// Add-on guid (<c>browser_specific_settings.gecko.id</c>).
    /// </summary>
    [MaxLength(GuidMaxLength)]
    public string Guid { get; set; }

    /// <summary>
    /// Slug of the add-on page.
    /// </summary>
    [MaxLength(SlugMaxLength)]
    public string Slug { get; set; }

    /// <summary>
    /// Add-on status.
    /// </summary>
    public FirefoxAddonStatus Status { get; set; }

    /// <summary>
    /// Whether the developer disabled the add-on (its versions are not offered).
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Default locale of the listing.
    /// </summary>
    [MaxLength(LocaleMaxLength)]
    public string DefaultLocale { get; set; }

    /// <summary>
    /// Version shown on the add-on page (<c>current_version</c>), null without one.
    /// </summary>
    [MaxLength(VersionMaxLength)]
    public string ListedVersion { get; set; }

    /// <summary>
    /// Newest version of any channel.
    /// </summary>
    [MaxLength(VersionMaxLength)]
    public string LatestVersion { get; set; }

    /// <summary>
    /// AMO id of <see cref="LatestVersion"/>.
    /// </summary>
    public long? LatestVersionId { get; set; }

    /// <summary>
    /// Channel of <see cref="LatestVersion"/>.
    /// </summary>
    public FirefoxVersionChannel LatestChannel { get; set; }

    /// <summary>
    /// File status of <see cref="LatestVersion"/>.
    /// </summary>
    public FirefoxFileStatus LatestFileStatus { get; set; }

    /// <summary>
    /// Average rating (1 to 5); null without ratings.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    /// Number of ratings.
    /// </summary>
    public long? RatingCount { get; set; }

    /// <summary>
    /// Average daily users AMO reports.
    /// </summary>
    public long? AverageDailyUsers { get; set; }

    /// <summary>
    /// Downloads in the last seven days AMO reports.
    /// </summary>
    public long? WeeklyDownloads { get; set; }

    /// <summary>
    /// Every state change of the add-on and its versions, oldest first.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<FirefoxStateChange> StatusHistory { get; set; } = [];

    /// <summary>
    /// UTC instant of the last successful sync.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Why the last sync could not read the add-on; null after a successful one.
    /// </summary>
    [MaxLength(SyncErrorMaxLength)]
    public string SyncError { get; set; }

    /// <summary>
    /// Whether a version waits for review: the schedule reads such add-ons every 15 minutes. Stored so the schedule can
    /// filter on it.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// The store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
