using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Assets;

namespace Storeasy.Application.Features.Assets.GetAssetDownloadUrl;

/// <summary>
/// Handles <see cref="GetAssetDownloadUrlQuery"/>. A file of another organization is reported as not found.
/// </summary>
/// <param name="assetRepository"></param>
/// <param name="fileStorage"></param>
/// <param name="storageOptions"></param>
/// <param name="timeProvider"></param>
public class GetAssetDownloadUrlQueryHandler(IStoreasyRepositoryBase<Asset> assetRepository,
                                             IFileStorage fileStorage,
                                             StorageOptions storageOptions,
                                             TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetAssetDownloadUrlQuery, AssetDownloadDto>
{
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = assetRepository;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly StorageOptions _storageOptions = storageOptions;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<AssetDownloadDto>> Handle(GetAssetDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsConfigured)
            return Response<AssetDownloadDto>.Error(null, MessageKey.StorageNotConfigured);

        var asset = await _assetRepository.GetFirstOrDefaultAsync(a => a.Id == request.AssetId && a.OrganizationId == request.OrganizationId,
                                                                  projection: a => new { a.ObjectKey, a.FileName, a.Status },
                                                                  cancellationToken: cancellationToken);

        if (asset == null)
            return Response<AssetDownloadDto>.Error(null, MessageKey.AssetNotFound);

        if (asset.Status != AssetStatus.Uploaded)
            return Response<AssetDownloadDto>.Error(null, MessageKey.AssetNotUploaded);

        var lifetime = TimeSpan.FromMinutes(_storageOptions.PresignedUrlLifetimeMinutes);

        return Response<AssetDownloadDto>.Success(new AssetDownloadDto
        {
            Url = _fileStorage.CreateDownloadUrl(asset.ObjectKey, lifetime, AssetRules.SafeFileName(asset.FileName)),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.Add(lifetime),
        });
    }
}
