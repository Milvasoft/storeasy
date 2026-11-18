namespace Storeasy.Application.Dtos.ReadinessDtos;

/// <summary>
/// One app of the organization overview: every store, its readiness and the next deadline.
/// </summary>
public class AppOverviewDto
{
    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Icon.
    /// </summary>
    public string IconUrl { get; set; }

    /// <summary>
    /// Store records.
    /// </summary>
    public List<AppOverviewStoreDto> Stores { get; set; }

    /// <summary>
    /// Nearest open deadline of the compliance timeline.
    /// </summary>
    public DeadlineDto NextDeadline { get; set; }
}
