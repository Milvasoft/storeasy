using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.StoreDtos;

namespace Storeasy.Application.Features.StoreConnections.GetStoreConnectionList;

/// <summary>
/// Lists an organization's store connections (any member; fingerprints only for the roles that manage connections).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetStoreConnectionListQuery : IQuery<List<StoreConnectionDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
