using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TestFlight.SubmitBetaReview;

/// <summary>
/// Submits a build to TestFlight beta app review (needed before external groups can test it). An explicit confirmation is
/// required: <see cref="Confirm"/> true and <see cref="ConfirmVersion"/> equal to the build's "version (build)" text.
/// Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record SubmitBetaReviewCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Build.
    /// </summary>
    public long BuildId { get; set; }

    /// <summary>
    /// The member confirmed the submission.
    /// </summary>
    public bool Confirm { get; set; }

    /// <summary>
    /// The build typed back as confirmation, e.g. <c>1.0 (37)</c>.
    /// </summary>
    public string ConfirmVersion { get; set; }
}
