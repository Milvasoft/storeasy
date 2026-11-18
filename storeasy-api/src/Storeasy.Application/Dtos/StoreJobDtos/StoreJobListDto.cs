using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Stores;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// A store job in the organization's job list.
/// </summary>
public class StoreJobListDto
{
    /// <summary>
    /// Job id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// What the job does.
    /// </summary>
    public StoreJobKind Kind { get; set; }

    /// <summary>
    /// Current state.
    /// </summary>
    public StoreJobStatus Status { get; set; }

    /// <summary>
    /// Store the job talks to.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// storeasy app the job works on.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Where the request came from.
    /// </summary>
    public ActionChannel Channel { get; set; }

    /// <summary>
    /// Requester ("system" for system jobs).
    /// </summary>
    public string RequestedByUserName { get; set; }

    /// <summary>
    /// Attempts started.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Attempts allowed.
    /// </summary>
    public int MaxAttempts { get; set; }

    /// <summary>
    /// Whether a stop was requested while it runs.
    /// </summary>
    public bool CancelRequested { get; set; }

    /// <summary>
    /// One line outcome.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Redacted reason of the last failed attempt.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// UTC instant the job was queued.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// UTC instant the first attempt started.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// UTC instant the job finished.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Projection from a job row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<StoreJob, StoreJobListDto>> Projection { get; } = j => new StoreJobListDto
    {
        Id = j.Id,
        Kind = j.Kind,
        Status = j.Status,
        Store = j.Store,
        AppId = j.AppId,
        Channel = j.Channel,
        RequestedByUserName = j.RequestedByUserName,
        Attempt = j.Attempt,
        MaxAttempts = j.MaxAttempts,
        CancelRequested = j.CancelRequested,
        Summary = j.Summary,
        Error = j.Error,
        CreationDate = j.CreationDate,
        StartDate = j.StartDate,
        EndDate = j.EndDate,
    };
}
