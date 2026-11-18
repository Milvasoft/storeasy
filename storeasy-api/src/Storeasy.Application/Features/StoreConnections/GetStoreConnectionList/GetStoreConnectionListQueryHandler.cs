using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreConnections.GetStoreConnectionList;

/// <summary>
/// Handles <see cref="GetStoreConnectionListQuery"/>. Every member sees which connections exist and whether they work (the
/// role matrix lets every role see apps and jobs, and editors pick a connection to sync); the fingerprint (key id, issuer
/// id suffix, service account e-mail) and the last verification error are shown only to the roles that manage
/// connections (<see cref="OrgPermission.ManageConnections"/>).
/// </summary>
/// <param name="connectionRepository"></param>
/// <param name="organizationAccess"></param>
public class GetStoreConnectionListQueryHandler(IStoreasyRepositoryBase<StoreConnection> connectionRepository, IOrganizationAccess organizationAccess) : IInterceptable, IQueryHandler<GetStoreConnectionListQuery, List<StoreConnectionDto>>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IOrganizationAccess _organizationAccess = organizationAccess;

    /// <inheritdoc/>
    public async Task<Response<List<StoreConnectionDto>>> Handle(GetStoreConnectionListQuery request, CancellationToken cancellationToken)
    {
        var connections = await _connectionRepository.GetAllAsync(c => c.OrganizationId == request.OrganizationId,
                                                                  projection: StoreConnectionDto.Projection,
                                                                  cancellationToken: cancellationToken);

        if (_organizationAccess.Current?.Allows(OrgPermission.ManageConnections) != true)
        {
            foreach (var connection in connections)
            {
                connection.Fingerprint = null;
                connection.LastVerifyError = null;
            }
        }

        return Response<List<StoreConnectionDto>>.Success([.. connections.OrderBy(c => c.Store).ThenBy(c => c.Name)]);
    }
}
