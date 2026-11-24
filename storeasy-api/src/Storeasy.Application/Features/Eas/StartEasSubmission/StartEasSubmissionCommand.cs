using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.StartEasSubmission;

/// <summary>
/// Submits a finished EAS build with EAS Submit: iOS to App Store Connect (TestFlight) with the project's App Store Connect API key on expo.dev, Android to a Play track with a release status and the project's Google service account key. Needs the typed confirmation (the app name and the platform word); the production track also needs the production release permission. Never automatic.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record StartEasSubmissionCommand : ICommand<EasSubmissionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy build id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Play track (Android): internal, alpha, beta or production.
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Play release status (Android): COMPLETED, DRAFT, IN_PROGRESS or HALTED.
    /// </summary>
    public string ReleaseStatus { get; set; }

    /// <summary>
    /// Typed confirmation.
    /// </summary>
    public string Confirm { get; set; }
}
