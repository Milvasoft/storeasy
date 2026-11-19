namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// The last known review state of one store version.
/// </summary>
public class ActivityBoardVersionDto
{
    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// User facing version (1.4).
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Build number attached to the version, if known.
    /// </summary>
    public string BuildNumber { get; set; }

    /// <summary>
    /// State as the store names it (e.g. <c>WAITING_FOR_REVIEW</c>, <c>IN_REVIEW</c>, <c>REJECTED</c>).
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// State before the last change.
    /// </summary>
    public string PreviousState { get; set; }

    /// <summary>
    /// UTC instant of the last change.
    /// </summary>
    public DateTime ChangedDate { get; set; }

    /// <summary>
    /// UTC instant the version was first seen in a submitted state.
    /// </summary>
    public DateTime? SubmittedDate { get; set; }
}
