namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// What the live activity page shows next to the moving jobs: versions with App Review, builds in processing or close to
/// their TestFlight expiry, and the last 24 hours of finished jobs and store events per store.
/// </summary>
public class ActivityBoardDto
{
    /// <summary>
    /// UTC instant the board was read; elapsed times and countdowns are relative to it.
    /// </summary>
    public DateTime GeneratedDate { get; set; }

    /// <summary>
    /// UTC start of the first hour of <see cref="Hours"/> (the hour 23 hours before the current one).
    /// </summary>
    public DateTime HoursStartDate { get; set; }

    /// <summary>
    /// The latest known state of each store record whose version is with App Review or changed state in the last 30 days,
    /// newest change first.
    /// </summary>
    public List<ActivityBoardVersionDto> Versions { get; set; } = [];

    /// <summary>
    /// Builds the store still processes and valid builds whose TestFlight expiry is within 14 days, processing first, then
    /// soonest expiry first.
    /// </summary>
    public List<ActivityBoardBuildDto> Builds { get; set; } = [];

    /// <summary>
    /// Hourly counts of the last 24 hours per store; hours without anything are left out.
    /// </summary>
    public List<ActivityBoardHourDto> Hours { get; set; } = [];
}
