using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Application.Features.ArtifactSources.SaveArtifactSource;

/// <summary>
/// Handles <see cref="SaveArtifactSourceCommand"/>. Not logged: the response carries the secret.
/// </summary>
/// <param name="SourceRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
public record SaveArtifactSourceCommandHandler(IStoreasyRepositoryBase<ArtifactPullSource> SourceRepository,
                                               IStoreasyRepositoryBase<App> AppRepository,
                                               ISecretProtector SecretProtector,
                                               ICurrentUser CurrentUser,
                                               IAuditTrail AuditTrail,
                                               TimeProvider TimeProvider) : IInterceptable, ICommandHandler<SaveArtifactSourceCommand, ArtifactSourceSavedDto>
{
    private readonly IStoreasyRepositoryBase<ArtifactPullSource> _sourceRepository = SourceRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<ArtifactSourceSavedDto>> Handle(SaveArtifactSourceCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var domains = request.AllowedDomains.Select(d => d.Trim().ToLowerInvariant()).Distinct().ToList();
        var platforms = request.Platforms.Distinct().ToList();

        if (request.Id is not { } id)
        {
            var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                                  projection: a => new App { Id = a.Id },
                                                                  cancellationToken: cancellationToken);

            if (app == null)
                return Response<ArtifactSourceSavedDto>.Error(null, MessageKey.AppNotFound);

            var publicId = "src_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
            var secret = SecureTokens.Create("whsec_");
            var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(secret), PullWebhooks.SecretContext(request.OrganizationId, publicId));

            var source = new ArtifactPullSource
            {
                OrganizationId = request.OrganizationId,
                AppId = request.AppId,
                Kind = request.Kind,
                PublicId = publicId,
                EncryptedSecret = sealedSecret.EncryptedSecret,
                EncryptedDataKey = sealedSecret.EncryptedDataKey,
                KeyVersion = sealedSecret.KeyVersion,
                AllowedDomains = domains,
                Platforms = platforms,
                IncludeApk = request.IncludeApk,
                SecretSetDate = now,
                SecretSetByUserName = _currentUser.UserName,
            };

            await _sourceRepository.AddAsync(source, cancellationToken);
            await AuditAsync(request.OrganizationId, source.Id, $"Pull source {publicId} ({request.Kind}) created.", cancellationToken);

            return Response<ArtifactSourceSavedDto>.Success(new ArtifactSourceSavedDto { Id = source.Id, Url = UrlOf(request.Kind, publicId), Secret = secret });
        }

        var existing = await _sourceRepository.GetFirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == request.OrganizationId,
                                                                      projection: s => new ArtifactPullSource { Id = s.Id, Kind = s.Kind, PublicId = s.PublicId },
                                                                      cancellationToken: cancellationToken);

        if (existing == null)
            return Response<ArtifactSourceSavedDto>.Error(null, MessageKey.ArtifactSourceNotFound);

        var builder = new SetPropertyBuilder<ArtifactPullSource>().SetPropertyValue(s => s.AllowedDomains, domains)
                                                                  .SetPropertyValue(s => s.Platforms, platforms)
                                                                  .SetPropertyValue(s => s.IncludeApk, request.IncludeApk);

        string rotated = null;

        if (request.RotateSecret)
        {
            rotated = SecureTokens.Create("whsec_");

            var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(rotated), PullWebhooks.SecretContext(request.OrganizationId, existing.PublicId));

            builder = builder.SetPropertyValue(s => s.EncryptedSecret, sealedSecret.EncryptedSecret)
                             .SetPropertyValue(s => s.EncryptedDataKey, sealedSecret.EncryptedDataKey)
                             .SetPropertyValue(s => s.KeyVersion, sealedSecret.KeyVersion)
                             .SetPropertyValue(s => s.SecretSetDate, now)
                             .SetPropertyValue(s => s.SecretSetByUserName, _currentUser.UserName);
        }

        await _sourceRepository.ExecuteUpdateAsync(existing.Id, builder, cancellationToken);
        await AuditAsync(request.OrganizationId, existing.Id, $"Pull source {existing.PublicId} updated{(rotated != null ? ", secret rotated" : string.Empty)}.", cancellationToken);

        return Response<ArtifactSourceSavedDto>.Success(new ArtifactSourceSavedDto { Id = existing.Id, Url = UrlOf(existing.Kind, existing.PublicId), Secret = rotated });
    }

    private Task AuditAsync(int organizationId, int sourceId, string summary, CancellationToken cancellationToken)
        => _auditTrail.WriteAsync(organizationId, AuditAction.ArtifactSourceUpdated, sourceId.ToString(), summary, cancellationToken: cancellationToken);

    private static string UrlOf(ArtifactPullSourceKind kind, string publicId) => kind == ArtifactPullSourceKind.Eas ? $"/api/webhooks/eas/{publicId}" : $"/api/webhooks/artifacts/{publicId}";
}
