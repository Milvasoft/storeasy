using Mapster;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetTrackedPackage;

/// <summary>
/// Handles <see cref="GetTrackedPackageQuery"/>. A package of another organization is reported as not found.
/// </summary>
/// <param name="packageRepository"></param>
/// <param name="versionRepository"></param>
/// <param name="alertRepository"></param>
public class GetTrackedPackageQueryHandler(IStoreasyRepositoryBase<TrackedPackage> packageRepository,
                                          IStoreasyRepositoryBase<TrackedPackageVersion> versionRepository,
                                          IStoreasyRepositoryBase<PackageAlert> alertRepository) : IInterceptable, IQueryHandler<GetTrackedPackageQuery, TrackedPackageDto>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<TrackedPackageVersion> _versionRepository = versionRepository;
    private readonly IStoreasyRepositoryBase<PackageAlert> _alertRepository = alertRepository;

    /// <inheritdoc/>
    public async Task<Response<TrackedPackageDto>> Handle(GetTrackedPackageQuery request, CancellationToken cancellationToken)
    {
        var summary = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == request.TrackedPackageId && p.OrganizationId == request.OrganizationId,
                                                                      projection: TrackedPackageSummaryDto.Projection,
                                                                      cancellationToken: cancellationToken);

        if (summary == null)
            return Response<TrackedPackageDto>.Error(null, MessageKey.TrackedPackageNotFound);

        var versions = await _versionRepository.GetAllAsync(v => v.TrackedPackageId == summary.Id && v.OrganizationId == request.OrganizationId,
                                                            projection: TrackedPackageVersionDto.Projection,
                                                            cancellationToken: cancellationToken);

        var alerts = await _alertRepository.GetAllAsync(a => a.TrackedPackageId == summary.Id && a.OrganizationId == request.OrganizationId,
                                                        projection: PackageAlertDto.Projection,
                                                        cancellationToken: cancellationToken);

        var dto = summary.Complete().Adapt<TrackedPackageDto>();

        dto.Versions = [.. versions.OrderByDescending(v => v.PublishedDate ?? DateTime.MinValue).ThenByDescending(v => v.Version, StringComparer.Ordinal)];
        dto.Alerts = PackageAlertDto.Order(alerts);

        return Response<TrackedPackageDto>.Success(dto);
    }
}
