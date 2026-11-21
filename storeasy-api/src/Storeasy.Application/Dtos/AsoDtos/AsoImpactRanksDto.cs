namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// Tracked keyword ranks of the store record before and after a change.
/// </summary>
public class AsoImpactRanksDto
{
    /// <summary>
    /// Keywords with a checked day in the windows.
    /// </summary>
    public int KeywordCount { get; set; }

    /// <summary>
    /// Keywords ranked in both windows (the averages below are over these).
    /// </summary>
    public int Compared { get; set; }

    /// <summary>
    /// Average of the compared keywords' before averages.
    /// </summary>
    public double? BeforeAverage { get; set; }

    /// <summary>
    /// Average of the compared keywords' after averages.
    /// </summary>
    public double? AfterAverage { get; set; }

    /// <summary>
    /// Keywords whose before average is in the top 10.
    /// </summary>
    public int BeforeTop10 { get; set; }

    /// <summary>
    /// Keywords whose after average is in the top 10.
    /// </summary>
    public int AfterTop10 { get; set; }

    /// <summary>
    /// Keywords at least one position higher after (or newly ranked).
    /// </summary>
    public int Improved { get; set; }

    /// <summary>
    /// Keywords at least one position lower after (or no longer ranked).
    /// </summary>
    public int Declined { get; set; }

    /// <summary>
    /// Every keyword, the largest moves first.
    /// </summary>
    public List<AsoImpactKeywordDto> Keywords { get; set; } = [];
}
