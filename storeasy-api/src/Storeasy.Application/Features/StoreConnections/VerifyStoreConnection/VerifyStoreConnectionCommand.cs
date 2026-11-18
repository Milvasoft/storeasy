using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.StoreDtos;

namespace Storeasy.Application.Features.StoreConnections.VerifyStoreConnection;

/// <summary>
/// Checks a connection against its store with one read call and stores the verdict.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record VerifyStoreConnectionCommand : ICommand<StoreConnectionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Connection id.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Google only, optional: a package name to prove the service account was invited to that app.
    /// </summary>
    public string PackageName { get; set; }
}
