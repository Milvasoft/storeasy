namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// The rank history of a tracked keyword, its followed competitors' histories and the latest top results.
/// </summary>
public class AsoKeywordRanksDto
{
    /// <summary>
    /// The tracked keyword.
    /// </summary>
    public AsoTrackedKeywordDto Keyword { get; set; }

    /// <summary>
    /// First day of the series.
    /// </summary>
    public DateOnly From { get; set; }

    /// <summary>
    /// Last day of the series.
    /// </summary>
    public DateOnly To { get; set; }

    /// <summary>
    /// One point per day, oldest first.
    /// </summary>
    public List<AsoRankPointDto> Points { get; set; } = [];

    /// <summary>
    /// The followed competitors' series.
    /// </summary>
    public List<AsoCompetitorSeriesDto> Competitors { get; set; } = [];

    /// <summary>
    /// The latest first ten results (from the last research or check).
    /// </summary>
    public List<AsoCompetitorDto> TopCompetitors { get; set; } = [];

    /// <summary>
    /// UTC instant the top results were read.
    /// </summary>
    public DateTime? ResultsDate { get; set; }
}
