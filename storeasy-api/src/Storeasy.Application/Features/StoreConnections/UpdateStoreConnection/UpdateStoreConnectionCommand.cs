using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.StoreDtos;

namespace Storeasy.Application.Features.StoreConnections.UpdateStoreConnection;

/// <summary>
/// Renames a store connection and sets or clears its icon. The secret is not touched (a Microsoft secret is replaced with
/// <c>RotateMsConnectionSecretCommand</c>). Answers the updated connection.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record UpdateStoreConnectionCommand : ICommand<StoreConnectionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Connection id.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Icon key (<see cref="Domain.Stores.StoreConnectionIcons"/>): a generic icon or the connection's own store logo.
    /// Null or empty clears it, so the store's text code shows again.
    /// </summary>
    public string Icon { get; set; }
}
