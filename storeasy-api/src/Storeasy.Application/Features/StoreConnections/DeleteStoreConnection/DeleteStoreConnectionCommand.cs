using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.StoreConnections.DeleteStoreConnection;

/// <summary>
/// Removes a store connection. The sealed secret is wiped from the row before the soft delete.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record DeleteStoreConnectionCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Connection id.
    /// </summary>
    public int ConnectionId { get; set; }
}
