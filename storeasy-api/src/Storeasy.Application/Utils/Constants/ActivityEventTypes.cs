namespace Storeasy.Application.Utils.Constants;

/// <summary>
/// Values of <see cref="Dtos.ActivityDtos.ActivityEventDto.Type"/>: what happened in an organization, pushed to its
/// members over the activity hub (<c>/hubs/activity</c>, client method <see cref="ClientMethod"/>).
/// </summary>
public static class ActivityEventTypes
{
    /// <summary>
    /// Name of the client method every event is sent to.
    /// </summary>
    public const string ClientMethod = "activity";

    /// <summary>
    /// A job was queued.
    /// </summary>
    public const string JobQueued = "job.queued";

    /// <summary>
    /// A job changed state without finishing (started, retry scheduled).
    /// </summary>
    public const string JobProgress = "job.progress";

    /// <summary>
    /// A job step started, progressed or ended.
    /// </summary>
    public const string JobStep = "job.step";

    /// <summary>
    /// A job finished successfully.
    /// </summary>
    public const string JobSucceeded = "job.succeeded";

    /// <summary>
    /// A job failed for good.
    /// </summary>
    public const string JobFailed = "job.failed";

    /// <summary>
    /// A job was cancelled.
    /// </summary>
    public const string JobCancelled = "job.cancelled";

    /// <summary>
    /// A store connection was checked against its store.
    /// </summary>
    public const string ConnectionVerified = "connection.verified";

    /// <summary>
    /// An app's store records and builds were refreshed from the stores.
    /// </summary>
    public const string AppSynced = "app.synced";

    /// <summary>
    /// A change plan was made and waits for approval.
    /// </summary>
    public const string PlanCreated = "plan.created";

    /// <summary>
    /// A change plan was approved, rejected or discarded.
    /// </summary>
    public const string PlanDecided = "plan.decided";

    /// <summary>
    /// The store accepted every change of a plan.
    /// </summary>
    public const string PlanApplied = "plan.applied";
    /// An inbound webhook event arrived.
    /// </summary>
    public const string WebhookReceived = "webhook.received";

    /// <summary>
    /// A rejection case was opened.
    /// </summary>
    public const string RejectionOpened = "rejection.opened";

    /// <summary>
    /// An App Store version changed state.
    /// </summary>
    public const string VersionStateChanged = "version.state";

    /// <summary>
    /// New store reviews arrived.
    /// </summary>
    public const string ReviewReceived = "review.received";

    /// <summary>
    /// A notification was created for the receiving user (sent to that user only).
    /// </summary>
    public const string NotificationCreated = "notification.created";
}
