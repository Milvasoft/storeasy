namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// One researched keyword: estimates computed from the store's public data (see <c>docs/aso-keywords.md</c>).
/// </summary>
public class AsoKeywordResearchDto
{
    /// <summary>
    /// Normalized keyword.
    /// </summary>
    public string Keyword { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Country (lower case).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Estimated popularity 0 to 100 (how early the store's autocomplete offers it); null when no source answered.
    /// </summary>
    public int? Popularity { get; set; }

    /// <summary>
    /// Source of the popularity (<c>apple-search-hints</c>, <c>google-play-suggest</c>, <c>apple-search-ads</c>).
    /// </summary>
    public string PopularitySource { get; set; }

    /// <summary>
    /// Estimated difficulty 0 to 100 (strength of the first ten results); null when the results carry no ratings.
    /// </summary>
    public int? Difficulty { get; set; }

    /// <summary>
    /// Opportunity 0 to 100 (geometric mean of popularity and 100 - difficulty); null when either is unknown.
    /// </summary>
    public int? Opportunity { get; set; }

    /// <summary>
    /// Number of apps the search returned (up to the depth read).
    /// </summary>
    public int? CompetingApps { get; set; }

    /// <summary>
    /// The app's position in the results, 1 based; null when not in the first <see cref="Depth"/> or not read.
    /// </summary>
    public int? CurrentRank { get; set; }

    /// <summary>
    /// Whether the search results were read, so a null <see cref="CurrentRank"/> means "not ranked".
    /// </summary>
    public bool RankChecked { get; set; }

    /// <summary>
    /// How many results were read.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Whether this keyword is already tracked for the store record and country.
    /// </summary>
    public bool Tracked { get; set; }

    /// <summary>
    /// The first ten results.
    /// </summary>
    public List<AsoCompetitorDto> TopCompetitors { get; set; } = [];

    /// <summary>
    /// Related terms the store suggested.
    /// </summary>
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
