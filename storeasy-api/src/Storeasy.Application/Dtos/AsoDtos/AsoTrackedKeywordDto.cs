namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// A tracked keyword with its current rank, its change and the last 30 days.
/// </summary>
public class AsoTrackedKeywordDto
{
    /// <summary>
    /// Tracking id.
    /// </summary>
    public int Id { get; set; }

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
    public string Keyword { get; set; }

    /// <summary>
    /// Country (lower case).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Whether competitors' ranks are recorded.
    /// </summary>
    public bool TrackCompetitors { get; set; }

    /// <summary>
    /// Competitors followed (store ids).
    /// </summary>
    public List<string> CompetitorIds { get; set; } = [];

    /// <summary>
    /// UTC instant tracking (last) started.
    /// </summary>
    public DateTime TrackedDate { get; set; }

    /// <summary>
    /// UTC instant of the last check; null before the first.
    /// </summary>
    public DateTime? LastCheckedDate { get; set; }

    /// <summary>
    /// Rank of the last check; null when not in the results (or not checked yet).
    /// </summary>
    public int? CurrentRank { get; set; }

    /// <summary>
    /// Change against the day before (positive = up); null when either day has no rank.
    /// </summary>
    public int? Change1 { get; set; }

    /// <summary>
    /// Change against 7 days before.
    /// </summary>
    public int? Change7 { get; set; }

    /// <summary>
    /// Change against 30 days before.
    /// </summary>
    public int? Change30 { get; set; }

    /// <summary>
    /// The last 30 days, oldest first: the rank, or null for a day not ranked or not checked.
    /// </summary>
    public List<int?> Sparkline { get; set; } = [];

    /// <summary>
    /// How many results the checks read.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Latest estimated popularity.
    /// </summary>
    public int? Popularity { get; set; }

    /// <summary>
    /// Latest estimated difficulty.
    /// </summary>
    public int? Difficulty { get; set; }

    /// <summary>
    /// Latest opportunity.
    /// </summary>
    public int? Opportunity { get; set; }
}
