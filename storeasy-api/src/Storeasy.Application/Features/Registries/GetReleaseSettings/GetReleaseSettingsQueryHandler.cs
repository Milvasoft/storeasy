using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetReleaseSettings;

/// <summary>
/// Handles <see cref="GetReleaseSettingsQuery"/>. A package or group of another organization is reported as not found.
/// </summary>
/// <param name="packageRepository"></param>
/// <param name="groupRepository"></param>
/// <param name="connectionRepository"></param>
public class GetReleaseSettingsQueryHandler(IStoreasyRepositoryBase<TrackedPackage> packageRepository,
                                           IStoreasyRepositoryBase<PackageGroup> groupRepository,
                                           IStoreasyRepositoryBase<RegistryConnection> connectionRepository) : IInterceptable, IQueryHandler<GetReleaseSettingsQuery, ReleaseSettingsDto>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<PackageGroup> _groupRepository = groupRepository;
    private readonly IStoreasyRepositoryBase<RegistryConnection> _connectionRepository = connectionRepository;

    /// <inheritdoc/>
    public async Task<Response<ReleaseSettingsDto>> Handle(GetReleaseSettingsQuery request, CancellationToken cancellationToken)
    {
        var dto = new ReleaseSettingsDto { TrackedPackageId = request.TrackedPackageId, PackageGroupId = request.PackageGroupId };

        if (request.TrackedPackageId is int packageId)
        {
            var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == packageId && p.OrganizationId == request.OrganizationId,
                                                                          projection: p => new TrackedPackage { Id = p.Id, LatestVersion = p.LatestVersion, Release = p.Release, PackageGroupId = p.PackageGroupId, PackageGroup = p.PackageGroup == null ? null : new PackageGroup { Id = p.PackageGroup.Id, Name = p.PackageGroup.Name, Release = p.PackageGroup.Release } },
                                                                          cancellationToken: cancellationToken);

            if (package == null)
                return Response<ReleaseSettingsDto>.Error(null, MessageKey.TrackedPackageNotFound);

            dto.FromGroup = package.Release == null && package.PackageGroup?.Release != null;
            dto.Settings = package.Release ?? package.PackageGroup?.Release;
            dto.PackageGroupId = package.PackageGroupId;
            dto.PackageGroupName = package.PackageGroup?.Name;
            dto.CurrentVersion = package.LatestVersion;
        }
        else
        {
            var group = await _groupRepository.GetFirstOrDefaultAsync(g => g.Id == request.PackageGroupId && g.OrganizationId == request.OrganizationId,
                                                                      projection: g => new PackageGroup { Id = g.Id, Name = g.Name, Release = g.Release, Packages = g.Packages.Select(p => new TrackedPackage { LatestVersion = p.LatestVersion }).ToList() },
                                                                      cancellationToken: cancellationToken);

            if (group == null)
                return Response<ReleaseSettingsDto>.Error(null, MessageKey.PackageGroupNotFound);

            dto.Settings = group.Release;
            dto.PackageGroupName = group.Name;
            dto.CurrentVersion = PackageGroups.Highest(group.Packages.Select(p => p.LatestVersion));
        }

        dto.Problems = ReleaseRules.Problems(dto.Settings);
        dto.NextVersions = [.. ReleaseRules.Next(dto.CurrentVersion, request.Prerelease).Select(v => new VersionSuggestionDto { Kind = v.Kind, Version = v.Version })];
        dto.GitHubConnected = (await _connectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == request.OrganizationId && c.Kind == RegistryConnectionKind.GitHub,
                                                                                  projection: c => (int?)c.Id,
                                                                                  cancellationToken: cancellationToken)).HasValue;

        return Response<ReleaseSettingsDto>.Success(dto);
    }
}
