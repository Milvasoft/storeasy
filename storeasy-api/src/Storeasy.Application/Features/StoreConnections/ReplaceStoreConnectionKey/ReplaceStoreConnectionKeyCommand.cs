using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.StoreDtos;

namespace Storeasy.Application.Features.StoreConnections.ReplaceStoreConnectionKey;

/// <summary>
/// Replaces the key of an existing App Store Connect or Google Play connection in place, so the connection id and
/// everything linked to it (apps, jobs, history) stay. An Apple key must belong to the same team (the same issuer id);
/// a Google service account may be another one. The new key is checked offline, verified against the store and only then
/// sealed in place of the old one; when verification fails the old key stays. A Microsoft secret is replaced with
/// <c>RotateMsConnectionSecretCommand</c>.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
[VerifiedEmailRequired]
public record ReplaceStoreConnectionKeyCommand : ICommand<StoreConnectionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Apple or Google connection.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// The new secret JSON, in the shape the connection was added with. Apple: <c>{"issuerId","keyId","privateKey"}</c>.
    /// Google: the service account JSON key file content. Masked in logs.
    /// </summary>
    [LogMasked]
    public string Secret { get; set; }
}
