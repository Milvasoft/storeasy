using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetTrackedPackageList;

/// <summary>
/// Handles <see cref="GetTrackedPackageListQuery"/>. Packages with open alerts come first, then by name.
/// </summary>
/// <param name="packageRepository"></param>
public class GetTrackedPackageListQueryHandler(IStoreasyRepositoryBase<TrackedPackage> packageRepository) : IInterceptable, IQueryHandler<GetTrackedPackageListQuery, List<TrackedPackageSummaryDto>>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;

    /// <inheritdoc/>
    public async Task<Response<List<TrackedPackageSummaryDto>>> Handle(GetTrackedPackageListQuery request, CancellationToken cancellationToken)
    {
        var items = await _packageRepository.GetAllAsync(p => p.OrganizationId == request.OrganizationId
                                                               && (request.Registry == null || p.Registry == request.Registry)
                                                               && (request.AppId == null || p.AppId == request.AppId),
                                                          projection: TrackedPackageSummaryDto.Projection,
                                                          cancellationToken: cancellationToken);

        return Response<List<TrackedPackageSummaryDto>>.Success([.. items.Select(i => i.Complete())
                                                                         .OrderByDescending(i => i.OpenAlertCount > 0)
                                                                         .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                                                                         .ThenBy(i => i.Registry)]);
    }
}
