using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Stores;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// A store job with its parameters, result and steps (the replayable timeline).
/// </summary>
public class StoreJobDetailDto : StoreJobListDto
{
    /// <summary>
    /// Parameters as JSON (never a secret).
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// Structured result as JSON.
    /// </summary>
    public string Result { get; set; }

    /// <summary>
    /// UTC instant from which a queued job may run (a retry waits for it).
    /// </summary>
    public DateTime NextAttemptDate { get; set; }

    /// <summary>
    /// Steps in order, across attempts.
    /// </summary>
    public List<StoreJobStepDto> Steps { get; set; }

    /// <summary>
    /// Projection from a job row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static new Expression<Func<StoreJob, StoreJobDetailDto>> Projection { get; } = j => new StoreJobDetailDto
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
        Payload = j.Payload,
        Result = j.Result,
        NextAttemptDate = j.NextAttemptDate,
        Steps = j.Steps.OrderBy(s => s.Order).Select(s => new StoreJobStepDto
        {
            Order = s.Order,
            Attempt = s.Attempt,
            Name = s.Name,
            Direction = s.Direction,
            Status = s.Status,
            Progress = s.Progress,
            Total = s.Total,
            Message = s.Message,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
        }).ToList(),
    };
}
