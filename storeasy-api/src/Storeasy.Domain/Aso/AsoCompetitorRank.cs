using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Aso;

/// <summary>
/// One day of a competitor's rank for a tracked keyword, recorded in the same check as <see cref="AsoKeywordRank"/>.
/// </summary>
[Table(TableNames.AsoCompetitorRanks)]
[Index(nameof(TrackedKeywordId), nameof(Date), nameof(CompetitorId), IsUnique = true)]
public class AsoCompetitorRank : BaseEntity<long>
{
    /// <summary>
    /// Column length of <see cref="CompetitorId"/>.
    /// </summary>
    public const int CompetitorIdMaxLength = 200;

    /// <summary>
    /// Column length of <see cref="CompetitorName"/>.
    /// </summary>
    public const int NameMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracking row.
    /// </summary>
    public int TrackedKeywordId { get; set; }

    /// <summary>
    /// UTC day of the check.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Store id of the competitor (App Store track id, Play package name).
    /// </summary>
    [MaxLength(CompetitorIdMaxLength)]
    public string CompetitorId { get; set; }

    /// <summary>
    /// Name the store showed that day, when known.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string CompetitorName { get; set; }

    /// <summary>
    /// Position, 1 based; null when not in the results read.
    /// </summary>
    public int? Rank { get; set; }
}
