using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.PreReviewDtos;

namespace Storeasy.Application.Features.PreReviews.StartPreReview;

/// <summary>
/// Starts an AI pre-review of one app in one store: Claude (the organization's Anthropic API key and its pre-review model)
/// reviews the version being prepared the way that store's reviewer would, as a background job
/// (<see cref="StoreJobKind.PreReview"/>). Returns the queued pre-review; the report arrives with the job
/// (<c>job.succeeded</c> on the activity hub, or read it again). Manual only; it never blocks or performs a submission. One
/// at a time per app and store. Needs the permission of preparing a submission's content.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record StartPreReviewCommand : ICommand<PreReviewDto>, IOrganizationRequest, IAppStoreRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store whose reviewer is imitated.
    /// </summary>
    public StoreKind Store { get; set; }
}
