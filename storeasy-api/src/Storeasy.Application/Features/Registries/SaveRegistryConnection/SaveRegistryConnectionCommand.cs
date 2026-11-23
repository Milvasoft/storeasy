using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Registries.SaveRegistryConnection;

/// <summary>
/// Saves (or replaces) the organization's credential of one kind: a GitHub fine-grained token for releases started in CI,
/// or a registry API key for registry write operations. The secret is sealed and never returned; a GitHub token is checked
/// with GitHub. An expiry date keeps a token reminder for registry keys.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record SaveRegistryConnectionCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// What the credential is for.
    /// </summary>
    public RegistryConnectionKind Kind { get; set; }

    /// <summary>
    /// The token or API key.
    /// </summary>
    [LogMasked]
    public string Secret { get; set; }

    /// <summary>
    /// Day it expires, as the registry or GitHub shows it.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
}
