namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// A followed competitor's rank series for a tracked keyword.
/// </summary>
public class AsoCompetitorSeriesDto
{
    /// <summary>
    /// Store id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Last known name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// One point per day, oldest first.
    /// </summary>
    public List<AsoRankPointDto> Points { get; set; } = [];
}
