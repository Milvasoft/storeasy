using Microsoft.EntityFrameworkCore;
using Milvasoft.Attributes.Annotations;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Stores;

/// <summary>
/// One unit of store work in the database backed queue (docs/urun-plani.md K4). The API's runner claims queued jobs with
/// <c>FOR UPDATE SKIP LOCKED</c>, so several instances never run the same job; a running job holds a lease
/// (<see cref="LockedUntil"/>) that is renewed while it reports steps, and a job whose lease ran out (its instance died)
/// is claimed again. <see cref="Payload"/> carries the job's parameters and never a secret: store secrets are opened only
/// inside the execution, from the connection row.
/// </summary>
[Table(TableNames.StoreJobs)]
[Index(nameof(OrganizationId), nameof(CreationDate), IsDescending = [false, true])]
[Index(nameof(Status), nameof(NextAttemptDate))]
[DontIndexCreationDate]
public class StoreJob : CreationAuditableEntity<long>
{
    /// <summary>
    /// Column length of <see cref="Error"/>.
    /// </summary>
    public const int ErrorMaxLength = 1000;

    /// <summary>
    /// Column length of <see cref="Summary"/>.
    /// </summary>
    public const int SummaryMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app the job works on, when it is about one app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Store the job talks to, when it is about one store.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// What the job does; selects the handler.
    /// </summary>
    public StoreJobKind Kind { get; set; }

    /// <summary>
    /// Parameters of the job as JSON. Never a secret.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Payload { get; set; }

    /// <summary>
    /// Structured result of a successful run as JSON.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Result { get; set; }

    /// <summary>
    /// One line, human readable outcome.
    /// </summary>
    [MaxLength(SummaryMaxLength)]
    public string Summary { get; set; }

    /// <summary>
    /// Current state.
    /// </summary>
    public StoreJobStatus Status { get; set; } = StoreJobStatus.Queued;

    /// <summary>
    /// Where the request came from.
    /// </summary>
    public ActionChannel Channel { get; set; } = ActionChannel.Web;

    /// <summary>
    /// User who asked for the job. Null for system jobs.
    /// </summary>
    public int? RequestedByUserId { get; set; }

    /// <summary>
    /// User name of the requester at the time ("system" for system jobs).
    /// </summary>
    [MaxLength(100)]
    public string RequestedByUserName { get; set; }

    /// <summary>
    /// Attempts started so far.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Attempts allowed before the job fails for good.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// UTC instant from which a queued job may be claimed (later than the creation for a retry with backoff).
    /// </summary>
    public DateTime NextAttemptDate { get; set; }

    /// <summary>
    /// UTC instant the first attempt started.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// UTC instant the job reached a final state.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// UTC instant the running attempt's lease expires. Null while not running.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// A member asked to stop the running job; the runner stops at the next step boundary.
    /// </summary>
    public bool CancelRequested { get; set; }

    /// <summary>
    /// Redacted, short reason of the last failed attempt.
    /// </summary>
    [MaxLength(ErrorMaxLength)]
    public string Error { get; set; }

    /// <summary>
    /// Steps of the job in order.
    /// </summary>
    [CascadeOnDelete]
    public virtual List<StoreJobStep> Steps { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
