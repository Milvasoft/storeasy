using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.StoreDtos;

namespace Storeasy.Application.Features.StoreConnections.ReplaceStoreConnectionKey;

/// <summary>
/// Replaces the key of an existing App Store Connect, Google Play or channel (winget and later) connection in place, so the connection id and
/// everything linked to it (apps, jobs, history) stay. An Apple key must belong to the same team (the same issuer id);
/// a Google service account may be another one; a channel's credential must be for the same account (the same winget-pkgs
/// fork). The new key is checked offline, verified against the store and only then
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
    /// Apple, Google or channel connection.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// The new secret JSON, in the shape the connection was added with. Apple: <c>{"issuerId","keyId","privateKey"}</c>.
    /// Google: the service account JSON key file content. winget: <c>{"token","fork"}</c>. Masked in logs.
    /// </summary>
    [LogMasked]
    public string Secret { get; set; }

    /// <summary>
    /// Expiry of the new credential, for stores that record one (a GitHub token); ignored otherwise.
    /// </summary>
    public DateTime? CredentialExpiryDate { get; set; }
}
