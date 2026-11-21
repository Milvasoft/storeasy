namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// A keyword research job of an app in one store: its state, how many keywords are measured so far and, once it
/// succeeded, the measurements.
/// </summary>
public class AsoKeywordResearchJobDto
{
    /// <summary>
    /// Job id (see <c>StoreJobs/job</c> for its steps).
    /// </summary>
    public long JobId { get; set; }

    /// <summary>
    /// Job status.
    /// </summary>
    public StoreJobStatus Status { get; set; }

    /// <summary>
    /// Whether a member asked a running job to stop.
    /// </summary>
    public bool CancelRequested { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Country (lower case).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// The normalized keywords asked for.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Keywords measured so far.
    /// </summary>
    public int Measured { get; set; }

    /// <summary>
    /// Keywords to measure.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Why the job failed, in the request's language when it is a known reason.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// UTC instant the job was queued.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// UTC instant the job ended.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// The measurements once the job succeeded (with the current tracking state); empty before.
    /// </summary>
    public List<AsoKeywordResearchDto> Items { get; set; } = [];
}
