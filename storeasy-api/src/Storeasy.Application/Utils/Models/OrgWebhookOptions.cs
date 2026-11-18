namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Outgoing webhook delivery settings (configuration section <see cref="SectionName"/>).
/// </summary>
public class OrgWebhookOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:OrgWebhooks";

    /// <summary>
    /// Whether this instance runs the delivery runner.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Pause of the runner when nothing is due, in milliseconds.
    /// </summary>
    public int PollIntervalMilliseconds { get; set; } = 2000;

    /// <summary>
    /// Attempts per delivery.
    /// </summary>
    public int MaxAttempts { get; set; } = 6;

    /// <summary>
    /// First retry delay in seconds; doubled per attempt, at most an hour.
    /// </summary>
    public int RetryBaseDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Timeout of one attempt in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Allows plain HTTP addresses (never in production; the private network block applies anyway).
    /// </summary>
    public bool AllowHttp { get; set; }

    /// <summary>
    /// Subscriptions an organization may have.
    /// </summary>
    public int MaxSubscriptions { get; set; } = 20;
}
