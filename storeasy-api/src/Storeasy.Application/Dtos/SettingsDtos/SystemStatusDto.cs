namespace Storeasy.Application.Dtos.SettingsDtos;

/// <summary>
/// The instance as a platform manager sees it: the build, each dependency and the job queues across organizations.
/// Secrets are never shown, only whether they are set.
/// </summary>
public class SystemStatusDto
{
    /// <summary>
    /// API version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Source commit of the build, when known.
    /// </summary>
    public string Commit { get; set; }

    /// <summary>
    /// Hosting environment name.
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// UTC instant of the check.
    /// </summary>
    public DateTime CheckedAt { get; set; }

    /// <summary>
    /// PostgreSQL.
    /// </summary>
    public DependencyStatusDto Database { get; set; }

    /// <summary>
    /// Redis (cache, revocation stamps, rate limits, SignalR backplane).
    /// </summary>
    public DependencyStatusDto Redis { get; set; }

    /// <summary>
    /// S3 compatible object storage.
    /// </summary>
    public DependencyStatusDto Storage { get; set; }

    /// <summary>
    /// E-mail provider (configured or not; its state shows in the outbox numbers).
    /// </summary>
    public DependencyStatusDto Mail { get; set; }

    /// <summary>
    /// Live events: <c>Redis</c> backplane (several instances) or <c>Local</c>.
    /// </summary>
    public string SignalRBackplane { get; set; }

    /// <summary>
    /// Whether the master key of the store secrets is set (the key itself is never shown).
    /// </summary>
    public bool EncryptionKeyConfigured { get; set; }

    /// <summary>
    /// Whether sign in with Google is configured.
    /// </summary>
    public bool GoogleSignInConfigured { get; set; }

    /// <summary>
    /// Whether web push is configured.
    /// </summary>
    public bool WebPushConfigured { get; set; }

    /// <summary>
    /// Store jobs across organizations.
    /// </summary>
    public JobQueueStatusDto Jobs { get; set; }

    /// <summary>
    /// E-mails waiting in the outbox.
    /// </summary>
    public int PendingEmails { get; set; }
}

/// <summary>
/// One dependency.
/// </summary>
public class DependencyStatusDto
{
    /// <summary>
    /// Whether it is configured.
    /// </summary>
    public bool Configured { get; set; }

    /// <summary>
    /// Whether it answered (null when not checked).
    /// </summary>
    public bool? Healthy { get; set; }

    /// <summary>
    /// Answer time of the check in milliseconds.
    /// </summary>
    public long? LatencyMs { get; set; }

    /// <summary>
    /// Provider or kind (e.g. Smtp), when it matters.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Short reason of a failure (redacted).
    /// </summary>
    public string Error { get; set; }
}

/// <summary>
/// Store job queue numbers across organizations.
/// </summary>
public class JobQueueStatusDto
{
    /// <summary>
    /// Waiting jobs.
    /// </summary>
    public int Queued { get; set; }

    /// <summary>
    /// Running jobs.
    /// </summary>
    public int Running { get; set; }

    /// <summary>
    /// Jobs that succeeded in the last 24 hours.
    /// </summary>
    public int SucceededLastDay { get; set; }

    /// <summary>
    /// Jobs that failed in the last 24 hours.
    /// </summary>
    public int FailedLastDay { get; set; }

    /// <summary>
    /// UTC instant the oldest waiting job became due, if any.
    /// </summary>
    public DateTime? OldestQueuedDate { get; set; }
}
