using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetPackageDownloads;

/// <summary>
/// Handles <see cref="GetPackageDownloadsQuery"/>. A package of another organization is reported as not found.
/// </summary>
/// <param name="packageRepository"></param>
/// <param name="versionRepository"></param>
/// <param name="dayRepository"></param>
/// <param name="timeProvider"></param>
public class GetPackageDownloadsQueryHandler(IStoreasyRepositoryBase<TrackedPackage> packageRepository,
                                            IStoreasyRepositoryBase<TrackedPackageVersion> versionRepository,
                                            IStoreasyRepositoryBase<PackageDownloadDay> dayRepository,
                                            TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetPackageDownloadsQuery, PackageDownloadsDto>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<TrackedPackageVersion> _versionRepository = versionRepository;
    private readonly IStoreasyRepositoryBase<PackageDownloadDay> _dayRepository = dayRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<PackageDownloadsDto>> Handle(GetPackageDownloadsQuery request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == request.TrackedPackageId && p.OrganizationId == request.OrganizationId,
                                                                      projection: p => new TrackedPackage
                                                                      {
                                                                          Id = p.Id,
                                                                          Registry = p.Registry,
                                                                          TotalDownloads = p.TotalDownloads,
                                                                          RecentDownloads = p.RecentDownloads,
                                                                          RecentDownloadsDays = p.RecentDownloadsDays,
                                                                          VersionDownloadsScope = p.VersionDownloadsScope,
                                                                          Likes = p.Likes,
                                                                          Points = p.Points,
                                                                          MaxPoints = p.MaxPoints,
                                                                          DownloadsSyncedDate = p.DownloadsSyncedDate,
                                                                      },
                                                                      cancellationToken: cancellationToken);

        if (package == null)
            return Response<PackageDownloadsDto>.Error(null, MessageKey.TrackedPackageNotFound);

        // One day earlier than the window, so the first day of the window can be derived from the snapshots.
        var since = _timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(-Math.Clamp(request.Days ?? RegistryDownloads.MaxDays, 1, RegistryDownloads.MaxDays) - 1);
        var days = await _dayRepository.GetAllAsync(d => d.TrackedPackageId == package.Id && d.OrganizationId == request.OrganizationId && d.Day >= since, cancellationToken: cancellationToken);
        var versions = await _versionRepository.GetAllAsync(v => v.TrackedPackageId == package.Id && v.OrganizationId == request.OrganizationId && v.Downloads != null,
                                                            projection: v => new VersionDownloadsDto { Version = v.Version, Downloads = v.Downloads.Value },
                                                            cancellationToken: cancellationToken);

        return Response<PackageDownloadsDto>.Success(new PackageDownloadsDto
        {
            TrackedPackageId = package.Id,
            Registry = package.Registry,
            Available = package.Registry != PackageRegistry.Maven,
            TotalDownloads = package.TotalDownloads,
            RecentDownloads = package.RecentDownloads,
            RecentDownloadsDays = package.RecentDownloadsDays,
            VersionScope = package.VersionDownloadsScope,
            Likes = package.Likes,
            Points = package.Points,
            MaxPoints = package.MaxPoints,
            DownloadsSyncedDate = package.DownloadsSyncedDate,
            Series = [.. RegistryDownloads.Series(days).Where(d => d.Day > since).Select(d => new DownloadDayDto { Day = d.Day, Downloads = d.Downloads })],
            Versions = [.. versions.OrderByDescending(v => v.Downloads).ThenByDescending(v => v.Version, StringComparer.Ordinal)],
        });
    }
}
