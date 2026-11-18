namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A change to the gradual rollout of a published submission.
/// </summary>
public class MsRolloutChangeDto
{
    /// <summary>
    /// Published submission.
    /// </summary>
    public string SubmissionId { get; set; }

    /// <summary>
    /// What to do.
    /// </summary>
    public MsRolloutAction Action { get; set; }

    /// <summary>
    /// New percentage (SetPercentage).
    /// </summary>
    public double? Percentage { get; set; }
}
