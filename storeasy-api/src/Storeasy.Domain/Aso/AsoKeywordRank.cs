using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Aso;

/// <summary>
/// One day of a tracked keyword's rank: at most one row per keyword and UTC day. A day without a row was not checked; a row
/// with a null <see cref="Rank"/> was checked and the app was not in the first <see cref="Depth"/> results. The store record,
/// keyword and country are kept on the row so the series is read without the tracking row.
/// </summary>
[Table(TableNames.AsoKeywordRanks)]
[Index(nameof(TrackedKeywordId), nameof(Date), IsUnique = true)]
[Index(nameof(StoreAppId), nameof(Keyword), nameof(Country), nameof(Date))]
public class AsoKeywordRank : BaseEntity<long>
{
    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracking row.
    /// </summary>
    public int TrackedKeywordId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Normalized keyword.
    /// </summary>
    [MaxLength(AsoTrackedKeyword.KeywordMaxLength)]
    public string Keyword { get; set; }

    /// <summary>
    /// Country, ISO 3166-1 alpha-2 lower case.
    /// </summary>
    [MaxLength(AsoTrackedKeyword.CountryLength)]
    public string Country { get; set; }

    /// <summary>
    /// UTC day of the check.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Position in the search results, 1 based; null when not in the first <see cref="Depth"/>.
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// How many results were read (the store may return fewer than asked).
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// UTC instant of the check.
    /// </summary>
    public DateTime CheckedDate { get; set; }
}
