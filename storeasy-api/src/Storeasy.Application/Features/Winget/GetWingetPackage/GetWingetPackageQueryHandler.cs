using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Stores;
using Storeasy.Domain.Winget;

namespace Storeasy.Application.Features.Winget.GetWingetPackage;

/// <summary>
/// Handles <see cref="GetWingetPackageQuery"/>. A record of another organization or another store is reported as not found.
/// </summary>
/// <param name="packageRepository"></param>
/// <param name="pullRequestRepository"></param>
/// <param name="connectionRepository"></param>
/// <param name="installerRepository"></param>
/// <param name="options"></param>
public class GetWingetPackageQueryHandler(IStoreasyRepositoryBase<WingetPackage> packageRepository,
                                          IStoreasyRepositoryBase<WingetPullRequest> pullRequestRepository,
                                          IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                          IStoreasyRepositoryBase<MsInstallerPackage> installerRepository,
                                          WingetOptions options) : IInterceptable, IQueryHandler<GetWingetPackageQuery, WingetPackageDto>
{
    private readonly IStoreasyRepositoryBase<WingetPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<WingetPullRequest> _pullRequestRepository = pullRequestRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IStoreasyRepositoryBase<MsInstallerPackage> _installerRepository = installerRepository;
    private readonly WingetOptions _options = options;

    /// <inheritdoc/>
    public async Task<Response<WingetPackageDto>> Handle(GetWingetPackageQuery request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.StoreAppId == request.StoreAppId && p.OrganizationId == request.OrganizationId && !p.StoreApp.App.IsDeleted,
                                                                      projection: WingetPackageDto.Projection,
                                                                      cancellationToken: cancellationToken);

        if (package == null)
            return Response<WingetPackageDto>.Error(null, MessageKey.StoreAppNotFound);

        if (package.ConnectionId is int connectionId)
        {
            var fingerprint = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == connectionId && c.OrganizationId == request.OrganizationId,
                                                                                 projection: c => new StoreConnection { Fingerprint = c.Fingerprint },
                                                                                 cancellationToken: cancellationToken);

            package.Fork = fingerprint?.Fingerprint?.Repository;
        }

        package.WrittenManifestVersion = WingetFields.DefaultManifestVersion;
        package.PackagePageUrl = WingetPaths.PackagePage(_options.Upstream, package.PackageIdentifier);

        var pullRequests = await _pullRequestRepository.GetAllAsync(p => p.StoreAppId == request.StoreAppId && p.OrganizationId == request.OrganizationId,
                                                                    projection: WingetPullRequestDto.Projection,
                                                                    cancellationToken: cancellationToken);

        package.PullRequests = [.. pullRequests.OrderByDescending(p => p.OpenedDate ?? DateTime.MinValue).ThenByDescending(p => p.Number)];

        // The same program's Microsoft Store MSI/EXE product: its installers are the usual source of a new winget version.
        var installers = await _installerRepository.GetAllAsync(i => i.OrganizationId == request.OrganizationId && i.StoreApp.AppId == package.AppId && i.StoreApp.Store == StoreKind.Microsoft,
                                                                projection: i => new MsInstallerPackage
                                                                {
                                                                    PackageUrl = i.PackageUrl,
                                                                    PackageType = i.PackageType,
                                                                    Architectures = i.Architectures,
                                                                    Languages = i.Languages,
                                                                    InstallerParameters = i.InstallerParameters,
                                                                    SignatureStatus = i.SignatureStatus,
                                                                },
                                                                cancellationToken: cancellationToken);

        package.InstallerSources = [.. installers.OrderBy(i => i.PackageUrl, StringComparer.Ordinal).Select(WingetInstallerSources.From)];

        return Response<WingetPackageDto>.Success(package);
    }
}
