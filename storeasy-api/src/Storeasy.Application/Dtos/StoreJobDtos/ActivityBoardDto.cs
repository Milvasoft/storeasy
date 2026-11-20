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
    /// and each Microsoft Store product whose submission waits for "Publish now" in Partner Center, newest change first.
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

    /// <summary>
    /// Apps of the organization whose alerts are muted, by id. Their versions and builds are left out of
    /// <see cref="Versions"/> and <see cref="Builds"/>; the web app leaves their rejection cases, deadlines and failed jobs
    /// out of the attention items too and says how many apps are muted.
    /// </summary>
    public List<int> MutedAppIds { get; set; } = [];
}
