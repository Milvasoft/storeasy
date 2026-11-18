namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Scheduled jobs of the events phase (configuration section <see cref="SectionName"/>): version state, review and
/// TestFlight syncs, deadline reminders and the rule watcher.
/// </summary>
public class EventScheduleOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Events";

    /// <summary>
    /// Whether this instance runs the schedules (turned off in the integration tests, which call the services directly).
    /// </summary>
    public bool SchedulesEnabled { get; set; } = true;
}
