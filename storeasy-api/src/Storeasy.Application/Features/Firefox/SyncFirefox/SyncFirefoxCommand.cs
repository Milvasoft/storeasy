using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Firefox.SyncFirefox;

/// <summary>
/// Queues a sync of one Firefox add-on: status, versions with their review status, rating and users. The schedule reads
/// add-ons with a version awaiting review every 15 minutes on its own.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record SyncFirefoxCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Firefox store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
