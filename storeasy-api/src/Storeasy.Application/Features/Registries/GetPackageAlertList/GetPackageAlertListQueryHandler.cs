using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetPackageAlertList;

/// <summary>
/// Handles <see cref="GetPackageAlertListQuery"/>.
/// </summary>
/// <param name="alertRepository"></param>
public class GetPackageAlertListQueryHandler(IStoreasyRepositoryBase<PackageAlert> alertRepository) : IInterceptable, IQueryHandler<GetPackageAlertListQuery, List<PackageAlertDto>>
{
    private readonly IStoreasyRepositoryBase<PackageAlert> _alertRepository = alertRepository;

    /// <inheritdoc/>
    public async Task<Response<List<PackageAlertDto>>> Handle(GetPackageAlertListQuery request, CancellationToken cancellationToken)
    {
        var alerts = await _alertRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId
                                                             && (request.TrackedPackageId == null || a.TrackedPackageId == request.TrackedPackageId)
                                                             && (request.IncludeResolved || a.ResolvedDate == null),
                                                        projection: PackageAlertDto.Projection,
                                                        cancellationToken: cancellationToken);

        return Response<List<PackageAlertDto>>.Success([.. PackageAlertDto.Order(alerts).Take(GetPackageAlertListQuery.MaxAlerts)]);
    }
}
