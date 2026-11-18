namespace Storeasy.Application.Dtos.ActivityDtos;

/// <summary>
/// One live event of an organization (see <see cref="ActivityEventTypes"/>). Carries ids, states and short redacted
/// messages only; never a secret.
/// </summary>
public class ActivityEventDto
{
    /// <summary>
    /// Event type, one of <see cref="ActivityEventTypes"/>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Organization the event belongs to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Job id for job events.
    /// </summary>
    public long? JobId { get; set; }

    /// <summary>
    /// Job kind for job events.
    /// </summary>
    public StoreJobKind? JobKind { get; set; }

    /// <summary>
    /// Job state after the event.
    /// </summary>
    public StoreJobStatus? JobStatus { get; set; }

    /// <summary>
    /// Attempt of the job the event belongs to.
    /// </summary>
    public int? Attempt { get; set; }

    /// <summary>
    /// Store involved.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// storeasy app involved.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Store connection involved.
    /// </summary>
    public int? ConnectionId { get; set; }

    /// <summary>
    /// Change plan, for plan events.
    /// </summary>
    public long? PlanId { get; set; }

    /// <summary>
    /// Step position for step events.
    /// </summary>
    public int? StepOrder { get; set; }

    /// <summary>
    /// Step name for step events.
    /// </summary>
    public string StepName { get; set; }

    /// <summary>
    /// Step state for step events.
    /// </summary>
    public StoreJobStepStatus? StepStatus { get; set; }

    /// <summary>
    /// Step direction for step events.
    /// </summary>
    public StepDirection? Direction { get; set; }

    /// <summary>
    /// Units done for progress events.
    /// </summary>
    public long? Progress { get; set; }

    /// <summary>
    /// Units in total for progress events.
    /// </summary>
    public long? Total { get; set; }

    /// <summary>
    /// Connection state for connection events.
    /// </summary>
    public ConnectionStatus? ConnectionStatus { get; set; }

    /// <summary>
    /// Short, redacted note.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// UTC instant of the event.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Kind of the record the event is about (rejection case, review, webhook event, notification...).
    /// </summary>
    public NotificationEntity? RelatedEntityType { get; set; }

    /// <summary>
    /// Id of that record.
    /// </summary>
    public string RelatedEntityId { get; set; }

    /// <summary>
    /// Notification id of a <c>notification.created</c> event.
    /// </summary>
    public long? NotificationId { get; set; }
}
