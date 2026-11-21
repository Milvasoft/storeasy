namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the store job runner (configuration section <see cref="SectionName"/>), bound once and registered as a
/// singleton.
/// </summary>
public class StoreJobOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Jobs";

    /// <summary>
    /// Whether this instance runs jobs. Every instance can enqueue; turn the runner off on instances that should not
    /// talk to the stores.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Jobs one instance runs at the same time.
    /// </summary>
    public int Workers { get; set; } = 4;

    /// <summary>
    /// Pause of an idle worker before it looks for a job again, in milliseconds.
    /// </summary>
    public int PollIntervalMilliseconds { get; set; } = 2000;

    /// <summary>
    /// Lease of a running attempt, in seconds. Renewed on every step report; a job whose lease runs out is claimed again.
    /// </summary>
    public int LeaseSeconds { get; set; } = 300;

    /// <summary>
    /// Attempts of a job when the request does not say.
    /// </summary>
    public int DefaultMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Delay before the first retry, in seconds; doubles with every further attempt.
    /// </summary>
    public int RetryBaseDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Longest delay between two attempts, in seconds.
    /// </summary>
    public int RetryMaxDelaySeconds { get; set; } = 900;
}
