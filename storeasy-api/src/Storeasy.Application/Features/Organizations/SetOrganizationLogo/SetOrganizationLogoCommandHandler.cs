using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.SetOrganizationLogo;

/// <summary>
/// Handles <see cref="SetOrganizationLogoCommand"/>. The storage is the source of truth: the stored size must equal the
/// declared one and the bytes must start with the signature of the declared type, then the image is redrawn by
/// <see cref="ILogoImageNormalizer"/> (a file that does not decode is refused). The redrawn PNG gets a fresh key, the
/// organization points at it, and the upload and the previous logo are deleted. A refused upload is deleted too.
/// </summary>
/// <param name="OrganizationRepository"></param>
/// <param name="AssetRepository"></param>
/// <param name="FileStorage"></param>
/// <param name="Normalizer"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
/// <param name="Logger"></param>
[Log]
public record SetOrganizationLogoCommandHandler(IStoreasyRepositoryBase<Organization> OrganizationRepository,
                                                IStoreasyRepositoryBase<Asset> AssetRepository,
                                                IFileStorage FileStorage,
                                                ILogoImageNormalizer Normalizer,
                                                IAuditTrail AuditTrail,
                                                TimeProvider TimeProvider,
                                                IMilvaLogger Logger) : IInterceptable, ICommandHandler<SetOrganizationLogoCommand, string>
{
    private const int _readChunkSize = 81920;

    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = OrganizationRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly IFileStorage _fileStorage = FileStorage;
    private readonly ILogoImageNormalizer _normalizer = Normalizer;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;
    private readonly IMilvaLogger _logger = Logger;

    /// <inheritdoc/>
    public async Task<Response<string>> Handle(SetOrganizationLogoCommand request, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsConfigured)
            return Response<string>.Error(null, MessageKey.StorageNotConfigured);

        var upload = await _assetRepository.GetFirstOrDefaultAsync(a => a.Id == request.AssetId
                                                                        && a.OrganizationId == request.OrganizationId
                                                                        && a.Purpose == AssetPurpose.OrganizationLogo
                                                                        && a.Status == AssetStatus.Pending,
                                                                   projection: a => new { a.Id, a.ObjectKey, a.ContentType, a.Size },
                                                                   cancellationToken: cancellationToken);

        if (upload == null)
            return Response<string>.Error(null, MessageKey.AssetNotFound);

        byte[] content;

        try
        {
            var stored = await _fileStorage.GetObjectInfoAsync(upload.ObjectKey, cancellationToken);

            if (stored == null)
                return Response<string>.Error(null, MessageKey.AssetNotUploaded);

            if (stored.Size != upload.Size || stored.Size > OrganizationLogoRules.MaxBytes)
            {
                await DiscardUploadAsync(upload.Id, upload.ObjectKey, cancellationToken);

                return Response<string>.Error(null, MessageKey.AssetSizeMismatch);
            }

            content = await ReadAsync(upload.ObjectKey, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warning("Logo upload {AssetId} could not be read: {Error}", upload.Id, SecretRedactor.Redact(ex.Message));

            return Response<string>.Error(null, MessageKey.AssetNotUploaded);
        }

        var signatureMatches = content.Length == upload.Size && OrganizationLogoRules.DetectContentType(content) == upload.ContentType;
        var png = signatureMatches ? _normalizer.Normalize(content) : null;

        if (png == null)
        {
            await DiscardUploadAsync(upload.Id, upload.ObjectKey, cancellationToken);

            return Response<string>.Error(null, MessageKey.OrganizationLogoInvalid);
        }

        var previousKey = (await _organizationRepository.GetFirstOrDefaultAsync(o => o.Id == request.OrganizationId,
                                                                                projection: o => new { o.LogoKey },
                                                                                cancellationToken: cancellationToken))?.LogoKey;

        var key = OrganizationLogoRules.ObjectKey(request.OrganizationId, Guid.NewGuid());

        using (var stream = new MemoryStream(png, writable: false))
            await _fileStorage.WriteAsync(key, stream, OrganizationLogoRules.OutputContentType, cancellationToken);

        var builder = new SetPropertyBuilder<Organization>().SetPropertyValue(o => o.LogoKey, key)
                                                            .SetPropertyValue(o => o.LogoUpdateDate, (DateTime?)_timeProvider.GetUtcNow().UtcDateTime);

        var updated = await _organizationRepository.ExecuteUpdateAsync(o => o.Id == request.OrganizationId, builder, cancellationToken);

        if (updated == 0)
        {
            await _fileStorage.DeleteAsync(key, cancellationToken);

            return Response<string>.Error(null, MessageKey.OrganizationNotFound);
        }

        await DiscardUploadAsync(upload.Id, upload.ObjectKey, cancellationToken);

        if (!string.IsNullOrEmpty(previousKey))
            await _fileStorage.DeleteAsync(previousKey, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.OrganizationLogoSet,
                                     request.OrganizationId.ToString(),
                                     previousKey == null ? "Organization logo set." : "Organization logo replaced.",
                                     details: new { UploadedBytes = upload.Size, upload.ContentType, StoredBytes = png.Length },
                                     cancellationToken: cancellationToken);

        return Response<string>.Success(_fileStorage.LogoUrl(key));
    }

    /// <summary>
    /// Reads at most one byte more than <see cref="OrganizationLogoRules.MaxBytes"/>, so a file that grew after the size
    /// check is noticed and refused instead of being read whole.
    /// </summary>
    private async Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken)
    {
        await using var source = await _fileStorage.OpenReadAsync(key, cancellationToken);
        using var buffer = new MemoryStream();

        var chunk = new byte[_readChunkSize];
        int read;

        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);

            if (buffer.Length > OrganizationLogoRules.MaxBytes)
                break;
        }

        return buffer.ToArray();
    }

    private async Task DiscardUploadAsync(long assetId, string key, CancellationToken cancellationToken)
    {
        await _fileStorage.DeleteAsync(key, cancellationToken);
        await _assetRepository.ExecuteDeleteAsync(a => a.Id == assetId, cancellationToken: cancellationToken);
    }
}
