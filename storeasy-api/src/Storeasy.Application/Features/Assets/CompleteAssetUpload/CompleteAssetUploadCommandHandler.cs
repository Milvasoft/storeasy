using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Assets;

namespace Storeasy.Application.Features.Assets.CompleteAssetUpload;

/// <summary>
/// Handles <see cref="CompleteAssetUploadCommand"/>. The storage is the source of truth for what was uploaded; the
/// declared size must match, so a client cannot store more than it was allowed to declare.
/// </summary>
/// <param name="AssetRepository"></param>
/// <param name="FileStorage"></param>
/// <param name="TimeProvider"></param>
/// <param name="Logger"></param>
[Log]
public record CompleteAssetUploadCommandHandler(IStoreasyRepositoryBase<Asset> AssetRepository,
                                                IFileStorage FileStorage,
                                                TimeProvider TimeProvider,
                                                IMilvaLogger Logger) : IInterceptable, ICommandHandler<CompleteAssetUploadCommand, AssetDto>
{
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly IFileStorage _fileStorage = FileStorage;
    private readonly TimeProvider _timeProvider = TimeProvider;
    private readonly IMilvaLogger _logger = Logger;

    /// <inheritdoc/>
    public async Task<Response<AssetDto>> Handle(CompleteAssetUploadCommand request, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsConfigured)
            return Response<AssetDto>.Error(null, MessageKey.StorageNotConfigured);

        var asset = await _assetRepository.GetFirstOrDefaultAsync(a => a.Id == request.AssetId && a.OrganizationId == request.OrganizationId,
                                                                  projection: a => new { a.Id, a.ObjectKey, a.Status, a.Size, a.UploadId },
                                                                  cancellationToken: cancellationToken);

        if (asset == null)
            return Response<AssetDto>.Error(null, MessageKey.AssetNotFound);

        if (asset.Status == AssetStatus.Pending)
        {
            var errorKey = await ConfirmAsync(asset.Id, asset.ObjectKey, asset.Size, asset.UploadId, request.Parts, cancellationToken);

            if (errorKey != null)
                return Response<AssetDto>.Error(null, errorKey);
        }
        else if (asset.Status != AssetStatus.Uploaded)
        {
            return Response<AssetDto>.Error(null, MessageKey.AssetNotUploaded);
        }

        var dto = await _assetRepository.GetFirstOrDefaultAsync(a => a.Id == asset.Id, projection: AssetDto.Projection, cancellationToken: cancellationToken);

        return Response<AssetDto>.Success(dto);
    }

    private async Task<string> ConfirmAsync(long assetId, string key, long declaredSize, string uploadId, List<UploadedPart> parts, CancellationToken cancellationToken)
    {
        StoredObjectInfo stored;

        try
        {
            if (uploadId != null)
            {
                if (parts == null || parts.Count == 0)
                    return MessageKey.AssetNotUploaded;

                await _fileStorage.CompleteMultipartUploadAsync(key, uploadId, parts, cancellationToken);
            }

            stored = await _fileStorage.GetObjectInfoAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warning("Upload of asset {AssetId} could not be confirmed: {Error}", assetId, SecretRedactor.Redact(ex.Message));

            return MessageKey.AssetNotUploaded;
        }

        if (stored == null)
            return MessageKey.AssetNotUploaded;

        if (stored.Size != declaredSize)
        {
            await _fileStorage.DeleteAsync(key, cancellationToken);

            await _assetRepository.ExecuteUpdateAsync(assetId,
                                                      new SetPropertyBuilder<Asset>().SetPropertyValue(a => a.Status, AssetStatus.Rejected)
                                                                                     .SetPropertyValue(a => a.UploadId, null),
                                                      cancellationToken);

            return MessageKey.AssetSizeMismatch;
        }

        await _assetRepository.ExecuteUpdateAsync(assetId,
                                                  new SetPropertyBuilder<Asset>().SetPropertyValue(a => a.Status, AssetStatus.Uploaded)
                                                                                 .SetPropertyValue(a => a.UploadId, null)
                                                                                 .SetPropertyValue(a => a.UploadDate, (DateTime?)_timeProvider.GetUtcNow().UtcDateTime),
                                                  cancellationToken);

        return null;
    }
}
