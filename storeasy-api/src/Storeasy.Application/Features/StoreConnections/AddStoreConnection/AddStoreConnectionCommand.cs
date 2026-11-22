using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.StoreConnections.AddStoreConnection;

/// <summary>
/// Adds store credentials to an organization. The secret is checked offline, sealed and stored; it is never returned.
/// Apple: <c>{"issuerId","keyId","privateKey"}</c>. Google: the service account JSON key file content. Microsoft:
/// <c>{"tenantId","clientId","clientSecret","sellerId"}</c> with <see cref="CredentialExpiryDate"/>. winget:
/// <c>{"token","fork"}</c>, a GitHub token and the owner/name of the winget-pkgs fork, optionally with the token expiry.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
[VerifiedEmailRequired]
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
    /// Optional icon key (<see cref="Domain.Stores.StoreConnectionIcons"/>): a generic icon or the store's own logo. Null or empty
    /// keeps the store's text code.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// The secret JSON. Masked in logs.
    /// </summary>
    [LogMasked]
    public string Secret { get; set; }

    /// <summary>
    /// UTC instant the credential expires (at most 24 months away): required for Microsoft (the Entra client secret, read
    /// by the expiry reminders), optional for winget (the GitHub token); ignored for the other stores.
    /// </summary>
    public DateTime? CredentialExpiryDate { get; set; }
}
