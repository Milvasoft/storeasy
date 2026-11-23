using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Registries.SyncTrackedPackage;

/// <summary>
/// Queues a sync now: of one tracked package, or of every package of the organization when no package is given. The
/// schedule syncs every package every six hours on its own.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SyncTrackedPackageCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracked package; null for every package.
    /// </summary>
    public int? TrackedPackageId { get; set; }
}
