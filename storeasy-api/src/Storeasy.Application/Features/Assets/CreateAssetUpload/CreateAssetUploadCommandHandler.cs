using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Assets;

namespace Storeasy.Application.Features.Assets.CreateAssetUpload;

/// <summary>
/// Handles <see cref="CreateAssetUploadCommand"/>. A multipart upload is started on the storage before the row is
/// written, so a storage failure leaves no pending row behind.
/// </summary>
/// <param name="AssetRepository"></param>
/// <param name="FileStorage"></param>
/// <param name="StorageOptions"></param>
/// <param name="TimeProvider"></param>
[Log]
public record CreateAssetUploadCommandHandler(IStoreasyRepositoryBase<Asset> AssetRepository,
                                              IFileStorage FileStorage,
                                              StorageOptions StorageOptions,
                                              TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CreateAssetUploadCommand, AssetUploadDto>
{
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly IFileStorage _fileStorage = FileStorage;
    private readonly StorageOptions _storageOptions = StorageOptions;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AssetUploadDto>> Handle(CreateAssetUploadCommand request, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsConfigured)
            return Response<AssetUploadDto>.Error(null, MessageKey.StorageNotConfigured);

        var lifetime = TimeSpan.FromMinutes(_storageOptions.PresignedUrlLifetimeMinutes);
        var key = AssetRules.ObjectKey(request.OrganizationId, request.Purpose, request.FileName, Guid.NewGuid());
        var multipart = AssetRules.IsMultipart(request.Size);
        var contentType = request.ContentType.Trim().ToLowerInvariant();

        var uploadId = multipart ? await _fileStorage.StartMultipartUploadAsync(key, contentType, cancellationToken) : null;

        var asset = new Asset
        {
            OrganizationId = request.OrganizationId,
            Purpose = request.Purpose,
            ObjectKey = key,
            FileName = request.FileName.Trim(),
            ContentType = contentType,
            Size = request.Size,
            Sha256 = request.Sha256?.ToLowerInvariant(),
            Status = AssetStatus.Pending,
            UploadId = uploadId,
        };

        await _assetRepository.AddAsync(asset, cancellationToken);

        var upload = new AssetUploadDto
        {
            AssetId = asset.Id,
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.Add(lifetime),
        };

        if (multipart)
        {
            upload.PartSize = AssetRules.PartSize(request.Size);
            upload.Parts = [.. Enumerable.Range(1, AssetRules.PartCount(request.Size)).Select(part => new AssetUploadPartDto { PartNumber = part, Url = _fileStorage.CreatePartUploadUrl(key, uploadId, part, lifetime) })];
        }
        else
        {
            upload.UploadUrl = _fileStorage.CreateUploadUrl(key, contentType, lifetime);
        }

        return Response<AssetUploadDto>.Success(upload);
    }
}
