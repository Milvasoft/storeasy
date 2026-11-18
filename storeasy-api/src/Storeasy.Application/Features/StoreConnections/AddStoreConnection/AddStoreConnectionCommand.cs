using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.StoreConnections.AddStoreConnection;

/// <summary>
/// Adds store credentials to an organization. The secret is checked offline, sealed and stored; it is never returned.
/// Apple: <c>{"issuerId","keyId","privateKey"}</c>. Google: the service account JSON key file content.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record AddStoreConnectionCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The secret JSON. Masked in logs.
    /// </summary>
    [LogMasked]
    public string Secret { get; set; }
}
