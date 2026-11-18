using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.Mockups.GetMockupExportZip;

/// <summary>
/// Handles <see cref="GetMockupExportZipQuery"/>. The URL downloads as <c>mockups-{jobId}.zip</c>.
/// </summary>
/// <param name="jobRepository"></param>
/// <param name="assetRepository"></param>
/// <param name="fileStorage"></param>
/// <param name="storageOptions"></param>
/// <param name="timeProvider"></param>
public class GetMockupExportZipQueryHandler(IStoreasyRepositoryBase<StoreJob> jobRepository,
                                            IStoreasyRepositoryBase<Asset> assetRepository,
                                            IFileStorage fileStorage,
                                            StorageOptions storageOptions,
                                            TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetMockupExportZipQuery, AssetDownloadDto>
{
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = assetRepository;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly StorageOptions _storageOptions = storageOptions;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<AssetDownloadDto>> Handle(GetMockupExportZipQuery request, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsConfigured)
            return Response<AssetDownloadDto>.Error(null, MessageKey.StorageNotConfigured);

        var (manifest, errorKey) = await _jobRepository.GetMockupExportManifestAsync(request.OrganizationId, request.JobId, cancellationToken);

        if (errorKey != null)
            return Response<AssetDownloadDto>.Error(null, errorKey);

        if (manifest.ZipAssetId is not long zipAssetId)
            return Response<AssetDownloadDto>.Error(null, MessageKey.MockupExportNotReady);

        var keys = await _assetRepository.GetUploadedAssetKeysAsync(request.OrganizationId, [zipAssetId], cancellationToken);

        if (!keys.TryGetValue(zipAssetId, out var key))
            return Response<AssetDownloadDto>.Error(null, MessageKey.MockupExportNotFound);

        var lifetime = TimeSpan.FromMinutes(_storageOptions.PresignedUrlLifetimeMinutes);

        return Response<AssetDownloadDto>.Success(new AssetDownloadDto
        {
            Url = _fileStorage.CreateDownloadUrl(key, lifetime, $"mockups-{request.JobId}.zip"),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.Add(lifetime),
        });
    }
}
