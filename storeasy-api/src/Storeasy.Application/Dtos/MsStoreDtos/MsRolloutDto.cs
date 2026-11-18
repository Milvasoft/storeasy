namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Gradual rollout of a submission.
/// </summary>
public class MsRolloutDto
{
    /// <summary>
    /// Whether the submission rolls out gradually.
    /// </summary>
    public bool IsPackageRollout { get; set; }

    /// <summary>
    /// Percentage of customers who get the packages.
    /// </summary>
    public double Percentage { get; set; }

    /// <summary>
    /// Rollout state.
    /// </summary>
    public MsRolloutStatus? Status { get; set; }

    /// <summary>
    /// Submission the other customers get.
    /// </summary>
    public string FallbackSubmissionId { get; set; }
}
