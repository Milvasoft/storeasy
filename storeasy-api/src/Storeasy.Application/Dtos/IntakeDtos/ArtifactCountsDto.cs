namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// How many packages arrived in a period and where they ended (intake overview counters).
/// </summary>
public class ArtifactCountsDto
{
    /// <summary>
    /// Start of the period (UTC, inclusive).
    /// </summary>
    public DateTime From { get; set; }

    /// <summary>
    /// End of the period (UTC, exclusive).
    /// </summary>
    public DateTime To { get; set; }

    /// <summary>
    /// Packages in the period.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Count per status; every status is listed, zero included.
    /// </summary>
    public List<ArtifactStatusCountDto> ByStatus { get; set; }

    /// <summary>
    /// Count per source (CI, REST, web, pull...), only the sources that occur.
    /// </summary>
    public List<ArtifactSourceCountDto> BySource { get; set; }
}

/// <summary>
/// Packages in one status.
/// </summary>
public class ArtifactStatusCountDto
{
    /// <summary>
    /// Status.
    /// </summary>
    public ArtifactSessionStatus Status { get; set; }

    /// <summary>
    /// Count.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Packages from one source.
/// </summary>
public class ArtifactSourceCountDto
{
    /// <summary>
    /// Source.
    /// </summary>
    public ArtifactSource Source { get; set; }

    /// <summary>
    /// Count.
    /// </summary>
    public int Count { get; set; }
}
