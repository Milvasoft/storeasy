using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.StoreJobs.CancelStoreJob;

/// <summary>
/// Stops a store job: a queued job is cancelled at once, a running one stops at its next step boundary. Returns the job
/// state after the request (Cancelled or Running).
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record CancelStoreJobCommand : ICommand<StoreJobStatus>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Job id.
    /// </summary>
    public long JobId { get; set; }
}
