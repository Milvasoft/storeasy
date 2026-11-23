using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetPackageGroupList;

/// <summary>
/// Handles <see cref="GetPackageGroupListQuery"/>.
/// </summary>
/// <param name="groupRepository"></param>
/// <param name="packageRepository"></param>
public class GetPackageGroupListQueryHandler(IStoreasyRepositoryBase<PackageGroup> groupRepository, IStoreasyRepositoryBase<TrackedPackage> packageRepository) : IInterceptable, IQueryHandler<GetPackageGroupListQuery, List<PackageGroupDto>>
{
    private readonly IStoreasyRepositoryBase<PackageGroup> _groupRepository = groupRepository;
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;

    /// <inheritdoc/>
    public async Task<Response<List<PackageGroupDto>>> Handle(GetPackageGroupListQuery request, CancellationToken cancellationToken)
    {
        var groups = await _groupRepository.GetAllAsync(g => g.OrganizationId == request.OrganizationId, projection: g => new PackageGroup { Id = g.Id, Name = g.Name }, cancellationToken: cancellationToken);
        var members = (await _packageRepository.GetAllAsync(p => p.OrganizationId == request.OrganizationId && p.PackageGroupId != null,
                                                            projection: TrackedPackageSummaryDto.Projection,
                                                            cancellationToken: cancellationToken))
                      .ToLookup(m => m.PackageGroupId);

        return Response<List<PackageGroupDto>>.Success([.. groups.Select(g => PackageGroupDto.From(g.Id, g.Name, members[g.Id])).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)]);
    }
}
