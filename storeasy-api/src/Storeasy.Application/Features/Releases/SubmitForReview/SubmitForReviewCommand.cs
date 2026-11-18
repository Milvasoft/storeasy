using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Content;

namespace Storeasy.Application.Features.Releases.SubmitForReview;

/// <summary>
/// Submits the App Store version for App Review behind the typed confirmation: the caller types the app name and the version ("Kumbara 1.0"); a mismatch writes nothing. The submission is a change plan approved at once and applied by a job. Only Owner and Admin (SubmitForReview).
/// </summary>
[VerifiedEmailRequired]
[OrgAuth(OrgPermission.SubmitForReview)]
public record SubmitForReviewCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Version; empty for the editable one.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// App name and version, typed.
    /// </summary>
    public string Confirm { get; set; }
}
