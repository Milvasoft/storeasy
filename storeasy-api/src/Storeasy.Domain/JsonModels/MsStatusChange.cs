using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One status a Microsoft Store submission was seen in, with the first time storeasy saw it.
/// </summary>
public class MsStatusChange
{
    /// <summary>
    /// Status.
    /// </summary>
    public MsSubmissionStatus Status { get; set; }

    /// <summary>
    /// UTC instant storeasy first saw the status.
    /// </summary>
    public DateTime Date { get; set; }
}
