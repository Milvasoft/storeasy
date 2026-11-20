using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Builds.GetBuildDownloadUrl;

/// <summary>
/// Handles <see cref="GetBuildDownloadUrlQuery"/>: finds the session that keeps the build's package
/// (<see cref="BuildPackages"/>), presigns a GET of its object for <see cref="StorageOptions.PresignedUrlLifetimeMinutes"/>
/// and writes an audit entry (never the URL: it is a bearer link). A build of another organization is reported as not found.
/// </summary>
/// <param name="buildRepository"></param>
/// <param name="sessionRepository"></param>
/// <param name="fileStorage"></param>
/// <param name="storageOptions"></param>
/// <param name="auditTrail"></param>
/// <param name="timeProvider"></param>
public class GetBuildDownloadUrlQueryHandler(IStoreasyRepositoryBase<Build> buildRepository,
                                             IStoreasyRepositoryBase<ArtifactSession> sessionRepository,
                                             IFileStorage fileStorage,
                                             StorageOptions storageOptions,
                                             IAuditTrail auditTrail,
                                             TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetBuildDownloadUrlQuery, BuildDownloadDto>
{
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly StorageOptions _storageOptions = storageOptions;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<BuildDownloadDto>> Handle(GetBuildDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var build = await _buildRepository.GetFirstOrDefaultAsync(b => b.Id == request.BuildId && b.OrganizationId == request.OrganizationId,
                                                                  projection: b => new Build
                                                                  {
                                                                      Id = b.Id,
                                                                      OrganizationId = b.OrganizationId,
                                                                      StoreAppId = b.StoreAppId,
                                                                      Store = b.Store,
                                                                      ExternalId = b.ExternalId,
                                                                      Version = b.Version,
                                                                      BuildNumber = b.BuildNumber,
                                                                  },
                                                                  cancellationToken: cancellationToken);

        if (build == null)
            return Response<BuildDownloadDto>.Error(null, MessageKey.BuildNotFound);

        if (!_fileStorage.IsConfigured)
            return Response<BuildDownloadDto>.Error(null, MessageKey.StorageNotConfigured);

        var session = await BuildPackages.FindAsync(_sessionRepository, build, cancellationToken);

        if (session == null)
            return Response<BuildDownloadDto>.Error(null, MessageKey.BuildPackageNotStored);

        var lifetime = TimeSpan.FromMinutes(Math.Max(1, _storageOptions.PresignedUrlLifetimeMinutes));
        var fileName = AssetRules.SafeFileName(session.FileName);

        var result = new BuildDownloadDto
        {
            BuildId = build.Id,
            SessionId = session.Id,
            Url = _fileStorage.CreateDownloadUrl(session.ObjectKey, lifetime, fileName),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.Add(lifetime),
            FileName = fileName,
            Size = session.Size,
            Sha256 = session.Sha256,
        };

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.BuildPackageDownloaded,
                                     build.Id.ToString(),
                                     $"Download link of build {build.Version} ({build.BuildNumber}) created: {fileName}.",
                                     build.Store,
                                     details: new { BuildId = build.Id, SessionId = session.Id, session.AppId, FileName = fileName, session.Size, session.Sha256 },
                                     cancellationToken: cancellationToken);

        return Response<BuildDownloadDto>.Success(result);
    }
}
