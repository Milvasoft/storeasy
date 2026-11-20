using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Finds the package storeasy keeps for a synced build. A build has no key of its own to an upload session: the session
/// names the store record it was matched to (<see cref="ArtifactSession.StoreAppId"/>) and, once delivered, the store's id of
/// the build (<see cref="ArtifactSession.StoreBuildId"/>: App Store Connect build id, Play version code). A build the store
/// got another way (the CI uploaded it itself while storeasy held the same file) is matched by its version and build number.
/// </summary>
public static class BuildPackages
{
    /// <summary>
    /// Session states whose package may be the build in the store: delivered, or held while the same file reached the store another way.
    /// </summary>
    public static readonly ArtifactSessionStatus[] UsableStatuses = [ArtifactSessionStatus.Delivered, ArtifactSessionStatus.Held];

    /// <summary>
    /// Whether <paramref name="session"/> holds the package of the build. Both must belong to the same store record.
    /// </summary>
    /// <param name="store">Store of the build.</param>
    /// <param name="externalId">Store's id of the build.</param>
    /// <param name="version">Version of the build.</param>
    /// <param name="buildNumber">Build number or version code of the build.</param>
    /// <param name="session"></param>
    /// <returns></returns>
    public static bool Matches(StoreKind store, string externalId, string version, string buildNumber, ArtifactSession session)
    {
        if (session == null)
            return false;

        if (!string.IsNullOrEmpty(session.StoreBuildId) && session.StoreBuildId == externalId)
            return true;

        var package = session.Package;

        if (package == null)
            return false;

        return store switch
        {
            // Play's build number is the version code; the version name may be missing (no release carries it yet).
            StoreKind.Google => Same(package.VersionCode, buildNumber) && (string.IsNullOrEmpty(version) || Same(package.VersionName, version)),
            // Partner Center reports the package version (1.2.3.0) as both version and build number.
            StoreKind.Microsoft => Same(package.VersionName, version) || Same(package.VersionCode, version),
            _ => Same(package.VersionName, version) && Same(package.VersionCode, buildNumber),
        };
    }

    /// <summary>
    /// Sets <see cref="BuildDto.PackageSessionId"/> of every build whose package storeasy still keeps (the newest matching session).
    /// </summary>
    /// <param name="sessionRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="builds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task FillAsync(IStoreasyRepositoryBase<ArtifactSession> sessionRepository, int organizationId, IReadOnlyCollection<BuildDto> builds, CancellationToken cancellationToken)
    {
        if (builds == null || builds.Count == 0)
            return;

        var storeAppIds = builds.Select(b => b.StoreAppId).Distinct().ToList();
        var sessions = await StoredSessionsAsync(sessionRepository, organizationId, storeAppIds, cancellationToken);

        if (sessions.Count == 0)
            return;

        foreach (var build in builds)
        {
            build.PackageSessionId = sessions.Where(s => s.StoreAppId == build.StoreAppId)
                                             .FirstOrDefault(s => Matches(build.Store, build.ExternalId, build.Version, build.BuildNumber, s))
                                             ?.Id;
        }
    }

    /// <summary>
    /// The newest session that keeps the package of <paramref name="build"/>, or null.
    /// </summary>
    /// <param name="sessionRepository"></param>
    /// <param name="build"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ArtifactSession> FindAsync(IStoreasyRepositoryBase<ArtifactSession> sessionRepository, Build build, CancellationToken cancellationToken)
    {
        var sessions = await StoredSessionsAsync(sessionRepository, build.OrganizationId, [build.StoreAppId], cancellationToken);

        return sessions.FirstOrDefault(s => Matches(build.Store, build.ExternalId, build.Version, build.BuildNumber, s));
    }

    private static async Task<List<ArtifactSession>> StoredSessionsAsync(IStoreasyRepositoryBase<ArtifactSession> sessionRepository,
                                                                         int organizationId,
                                                                         List<int> storeAppIds,
                                                                         CancellationToken cancellationToken)
    {
        var sessions = await sessionRepository.GetAllAsync(s => s.OrganizationId == organizationId
                                                                && s.StoreAppId != null
                                                                && storeAppIds.Contains(s.StoreAppId.Value)
                                                                && !s.PackageDeleted
                                                                && s.ObjectKey != null
                                                                && UsableStatuses.Contains(s.Status),
                                                           projection: s => new ArtifactSession
                                                           {
                                                               Id = s.Id,
                                                               OrganizationId = s.OrganizationId,
                                                               AppId = s.AppId,
                                                               StoreAppId = s.StoreAppId,
                                                               Status = s.Status,
                                                               StoreBuildId = s.StoreBuildId,
                                                               Package = s.Package,
                                                               ObjectKey = s.ObjectKey,
                                                               FileName = s.FileName,
                                                               Size = s.Size,
                                                               Sha256 = s.Sha256,
                                                               Platform = s.Platform,
                                                           },
                                                           cancellationToken: cancellationToken);

        // Delivered before held, then the newest first.
        return [.. sessions.OrderBy(s => s.Status == ArtifactSessionStatus.Delivered ? 0 : 1).ThenByDescending(s => s.Id)];
    }

    private static bool Same(string left, string right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
