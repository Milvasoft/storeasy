using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Apps.SyncApps;

/// <summary>
/// Queues a sync of apps and builds: for one connection, or for every connection of the organization whose store the
/// engine reads (App Store Connect, Google Play). Returns the job ids; follow them on the activity hub.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SyncAppsCommand : ICommand<List<long>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Connection to sync; null for all.
    /// </summary>
    public int? ConnectionId { get; set; }
}
