namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// One step of a store job.
/// </summary>
public class StoreJobStepDto
{
    /// <summary>
    /// Position in the job.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Attempt the step belongs to.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Machine name (<c>area.action</c>).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Which way data moves.
    /// </summary>
    public StepDirection Direction { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public StoreJobStepStatus Status { get; set; }

    /// <summary>
    /// Units done.
    /// </summary>
    public long? Progress { get; set; }

    /// <summary>
    /// Units in total.
    /// </summary>
    public long? Total { get; set; }

    /// <summary>
    /// Short note.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// UTC start.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// UTC end.
    /// </summary>
    public DateTime? EndDate { get; set; }
}
