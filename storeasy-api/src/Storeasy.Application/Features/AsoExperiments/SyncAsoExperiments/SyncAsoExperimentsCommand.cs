using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.AsoExperiments.SyncAsoExperiments;

/// <summary>
/// Queues a read of an App Store record's experiments and custom product pages (states, dates, addresses; ones made in
/// App Store Connect get a record). Returns the job id. Editors and above.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SyncAsoExperimentsCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }
}
