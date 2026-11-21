using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Aso;

/// <summary>
/// A search keyword an organization tracks for one store record in one country. The daily rank check records one
/// <see cref="AsoKeywordRank"/> per day while <see cref="IsActive"/>; untracking only stops the checks, so the rank history
/// stays (a later before/after report reads it) and tracking the same keyword again continues the same series.
/// </summary>
[Table(TableNames.AsoTrackedKeywords)]
[Index(nameof(StoreAppId), nameof(Keyword), nameof(Country), IsUnique = true)]
[Index(nameof(OrganizationId), nameof(IsActive))]
[Index(nameof(IsActive), nameof(LastCheckedDate))]
public class AsoTrackedKeyword : AuditableEntity<int>
{
    /// <summary>
    /// Longest keyword (Apple's whole keywords field is 100 characters).
    /// </summary>
    public const int KeywordMaxLength = 100;

    /// <summary>
    /// Column length of <see cref="Country"/> (ISO 3166-1 alpha-2, lower case).
    /// </summary>
    public const int CountryLength = 2;

    /// <summary>
    /// Most competitors followed per keyword.
    /// </summary>
    public const int MaxCompetitors = 10;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record whose rank is checked.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The keyword, normalized (trimmed, single spaces, lower case in the country's language).
    /// </summary>
    [MaxLength(KeywordMaxLength)]
    public string Keyword { get; set; }

    /// <summary>
    /// Storefront country, ISO 3166-1 alpha-2 in lower case (e.g. <c>tr</c>, <c>us</c>).
    /// </summary>
    [MaxLength(CountryLength)]
    public string Country { get; set; }

    /// <summary>
    /// Whether the ranks of <see cref="CompetitorIds"/> are recorded too.
    /// </summary>
    public bool TrackCompetitors { get; set; }

    /// <summary>
    /// Store ids of the competitors followed (App Store track ids, Play package names). When competitors are tracked and
    /// none were chosen, the first check fills it with the top five results other than the app itself, so each competitor
    /// keeps one continuous series.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> CompetitorIds { get; set; } = [];

    /// <summary>
    /// Whether the daily check runs.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// UTC instant it was (last) started to be tracked.
    /// </summary>
    public DateTime TrackedDate { get; set; }

    /// <summary>
    /// UTC instant it was untracked; null while active.
    /// </summary>
    public DateTime? UntrackedDate { get; set; }

    /// <summary>
    /// UTC instant of the last successful rank check.
    /// </summary>
    public DateTime? LastCheckedDate { get; set; }

    /// <summary>
    /// Rank of the last check (1 based); null when the app was not in the results read.
    /// </summary>
    public int? LastRank { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
