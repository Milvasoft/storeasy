using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Aso;

/// <summary>
/// The latest measurement of a keyword in one store and country, computed from public store data (search suggestions and
/// search results). It belongs to no organization: the store's public answer is the same for everyone, so one measurement
/// serves every research and is refreshed when it is older than the configured freshness. Scores are estimates; see
/// <c>docs/aso-keywords.md</c> for how each one is computed.
/// </summary>
[Table(TableNames.AsoKeywordMetrics)]
[Index(nameof(Store), nameof(Country), nameof(Keyword), IsUnique = true)]
public class AsoKeywordMetric : BaseEntity<long>
{
    /// <summary>
    /// Column length of <see cref="PopularitySource"/>.
    /// </summary>
    public const int SourceMaxLength = 40;

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Country, ISO 3166-1 alpha-2 lower case.
    /// </summary>
    [MaxLength(AsoTrackedKeyword.CountryLength)]
    public string Country { get; set; }

    /// <summary>
    /// Normalized keyword.
    /// </summary>
    [MaxLength(AsoTrackedKeyword.KeywordMaxLength)]
    public string Keyword { get; set; }

    /// <summary>
    /// Estimated popularity, 0 to 100; null when no suggestion source answered.
    /// </summary>
    public int? Popularity { get; set; }

    /// <summary>
    /// Where the popularity came from (<c>apple-search-hints</c>, <c>google-play-suggest</c>, <c>apple-search-ads</c>).
    /// </summary>
    [MaxLength(SourceMaxLength)]
    public string PopularitySource { get; set; }

    /// <summary>
    /// Estimated difficulty, 0 to 100; null when the search results could not be read with ratings.
    /// </summary>
    public int? Difficulty { get; set; }

    /// <summary>
    /// Number of apps the search returned (capped by the depth read).
    /// </summary>
    public int? CompetingApps { get; set; }

    /// <summary>
    /// The first ten results.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<AsoCompetitor> TopCompetitors { get; set; } = [];

    /// <summary>
    /// Related search terms the suggestions returned.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> RelatedTerms { get; set; } = [];

    /// <summary>
    /// UTC instant the popularity was measured.
    /// </summary>
    public DateTime? PopularityDate { get; set; }

    /// <summary>
    /// UTC instant the search results were read.
    /// </summary>
    public DateTime? ResultsDate { get; set; }
}
