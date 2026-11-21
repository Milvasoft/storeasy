using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.StoreMetrics.SyncStoreMetrics;

/// <summary>
/// Reads the store page metrics now: one job per store record with a connection, of the app or of every app. They are
/// also read once a day. Only reads; returns the job ids.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SyncStoreMetricsCommand : ICommand<List<long>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only this app.
    /// </summary>
    public int? AppId { get; set; }
}
