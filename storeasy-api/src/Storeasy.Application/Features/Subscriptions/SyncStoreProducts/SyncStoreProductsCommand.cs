using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Subscriptions.SyncStoreProducts;

/// <summary>
/// Queues a sync of the subscriptions and In-App Purchases of a store record; returns the job id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SyncStoreProductsCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
