namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A Microsoft Store task without an API: done by the user (or a browser agent) in Partner Center, with the page to open.
/// </summary>
public class MsAssistedStepDto
{
    /// <summary>
    /// Step key (ms.app.reserve-name, ms.app.first-submission...).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Localized title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Reason, for items the API refuses.
    /// </summary>
    public MsAssistedReason? Reason { get; set; }

    /// <summary>
    /// Partner Center page.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Whether the step still blocks API work.
    /// </summary>
    public bool Pending { get; set; }

    /// <summary>
    /// Row of the step in the assisted steps table (complete or hand it off through the assisted step endpoints); null
    /// until the step came up.
    /// </summary>
    public int? StepId { get; set; }

    /// <summary>
    /// Recorded progress of the step.
    /// </summary>
    public AssistedStepStatus? Status { get; set; }
}
