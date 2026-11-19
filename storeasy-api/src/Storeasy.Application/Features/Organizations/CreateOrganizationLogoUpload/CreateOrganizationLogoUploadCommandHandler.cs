using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Assets;

namespace Storeasy.Application.Features.Organizations.CreateOrganizationLogoUpload;

/// <summary>
/// Handles <see cref="CreateOrganizationLogoUploadCommand"/>: a pending <see cref="AssetPurpose.OrganizationLogo"/> asset
/// and one presigned PUT URL. An upload nobody confirms is removed by the intake cleanup like any pending asset.
/// </summary>
/// <param name="AssetRepository"></param>
/// <param name="FileStorage"></param>
/// <param name="StorageOptions"></param>
/// <param name="TimeProvider"></param>
[Log]
public record CreateOrganizationLogoUploadCommandHandler(IStoreasyRepositoryBase<Asset> AssetRepository,
                                                         IFileStorage FileStorage,
                                                         StorageOptions StorageOptions,
                                                         TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CreateOrganizationLogoUploadCommand, AssetUploadDto>
{
    private const string _fileName = "logo";

    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly IFileStorage _fileStorage = FileStorage;
    private readonly StorageOptions _storageOptions = StorageOptions;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AssetUploadDto>> Handle(CreateOrganizationLogoUploadCommand request, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsConfigured)
            return Response<AssetUploadDto>.Error(null, MessageKey.StorageNotConfigured);

        var lifetime = TimeSpan.FromMinutes(_storageOptions.PresignedUrlLifetimeMinutes);
        var key = AssetRules.ObjectKey(request.OrganizationId, AssetPurpose.OrganizationLogo, _fileName, Guid.NewGuid());
        var contentType = request.ContentType.Trim().ToLowerInvariant();

        var asset = new Asset
        {
            OrganizationId = request.OrganizationId,
            Purpose = AssetPurpose.OrganizationLogo,
            ObjectKey = key,
            FileName = _fileName,
            ContentType = contentType,
            Size = request.Size,
            Status = AssetStatus.Pending,
        };

        await _assetRepository.AddAsync(asset, cancellationToken);

        return Response<AssetUploadDto>.Success(new AssetUploadDto
        {
            AssetId = asset.Id,
            UploadUrl = _fileStorage.CreateUploadUrl(key, contentType, lifetime),
            ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.Add(lifetime),
        });
    }
}
