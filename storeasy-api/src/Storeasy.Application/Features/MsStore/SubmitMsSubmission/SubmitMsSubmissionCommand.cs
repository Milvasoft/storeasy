using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.MsStore.SubmitMsSubmission;

/// <summary>
/// Sends a Microsoft Store draft on to certification: commits the pending app, flight or add-on submission storeasy
/// created (MSIX), or submits the draft of an MSI/EXE product. Needs <see cref="OrgPermission.SubmitForReview"/> and a
/// typed confirmation (the product's Store id). Refused when there is no draft, the draft was changed in Partner Center,
/// another submission is in progress, or (MSI/EXE) an installer is not Authenticode signed. The commit and the status
/// polling run as a store job; returns its id.
/// </summary>
[OrgAuth(OrgPermission.SubmitForReview)]
public record SubmitMsSubmissionCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// What to submit (App, Flight, Addon; MSI/EXE products always submit the product).
    /// </summary>
    public MsSubmissionTarget Target { get; set; } = MsSubmissionTarget.App;

    /// <summary>
    /// Flight of a flight submission.
    /// </summary>
    public string FlightId { get; set; }

    /// <summary>
    /// Add-on of an add-on submission.
    /// </summary>
    public string AddonStoreId { get; set; }

    /// <summary>
    /// Typed confirmation: the product's Store id.
    /// </summary>
    public string Confirm { get; set; }
}
