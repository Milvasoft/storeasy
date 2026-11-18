using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Intake;
using System.Text;
using System.Text.Json;

namespace Storeasy.Application.Features.ArtifactSources.ReceiveArtifactWebhook;

/// <summary>
/// Handles <see cref="ReceiveArtifactWebhookCommand"/>.
/// </summary>
/// <param name="SourceRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="IntakeService"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
public record ReceiveArtifactWebhookCommandHandler(IStoreasyRepositoryBase<ArtifactPullSource> SourceRepository,
                                                   ISecretProtector SecretProtector,
                                                   IArtifactIntakeService IntakeService,
                                                   IMilvaLocalizer Localizer,
                                                   TimeProvider TimeProvider) : IInterceptable, ICommandHandler<ReceiveArtifactWebhookCommand, ArtifactUploadDto>
{
    private readonly IStoreasyRepositoryBase<ArtifactPullSource> _sourceRepository = SourceRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IArtifactIntakeService _intakeService = IntakeService;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<ArtifactUploadDto>> Handle(ReceiveArtifactWebhookCommand request, CancellationToken cancellationToken)
    {
        var source = await _sourceRepository.GetFirstOrDefaultAsync(s => s.PublicId == request.SourceId && s.Kind == ArtifactPullSourceKind.Webhook,
                                                                    projection: s => new ArtifactPullSource
                                                                    {
                                                                        Id = s.Id,
                                                                        OrganizationId = s.OrganizationId,
                                                                        AppId = s.AppId,
                                                                        PublicId = s.PublicId,
                                                                        EncryptedSecret = s.EncryptedSecret,
                                                                        EncryptedDataKey = s.EncryptedDataKey,
                                                                        KeyVersion = s.KeyVersion,
                                                                        AllowedDomains = s.AllowedDomains,
                                                                        Platforms = s.Platforms,
                                                                        IncludeApk = s.IncludeApk,
                                                                    },
                                                                    cancellationToken: cancellationToken);

        if (source == null)
            return Response<ArtifactUploadDto>.Error(null, MessageKey.ArtifactSourceNotFound);

        var secretBytes = _secretProtector.Unprotect(new ProtectedSecret(source.EncryptedSecret, source.EncryptedDataKey, source.KeyVersion), PullWebhooks.SecretContext(source.OrganizationId, source.PublicId));
        var secret = Encoding.UTF8.GetString(secretBytes);

        Array.Clear(secretBytes);

        if (!PullWebhooks.IsValidArtifactSignature(secret, request.RawBody, request.Timestamp, request.Signature, _timeProvider.GetUtcNow().UtcDateTime))
        {
            await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, "Invalid signature or timestamp", cancellationToken);

            return Response<ArtifactUploadDto>.Error(null, MessageKey.WebhookSignatureInvalid);
        }

        ArtifactReadyBody body;

        try
        {
            body = JsonSerializer.Deserialize<ArtifactReadyBody>(request.RawBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            body = null;
        }

        var problem = body == null ? "JSON"
                      : body.Schema != PullWebhooks.ArtifactReadySchema ? "schema"
                      : body.AppId != source.AppId ? "appId"
                      : string.IsNullOrWhiteSpace(body.FileName) || IntakeRules.Classify(body.FileName) == null ? "fileName"
                      : !IntakeRules.IsSha256(body.Sha256?.ToLowerInvariant()) ? "sha256"
                      : null;

        if (problem != null)
        {
            await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, $"Invalid body ({problem})", cancellationToken);

            return Response<ArtifactUploadDto>.Error(null, new ResponseMessage(MessageKey.WebhookPayloadInvalid, _localizer[MessageKey.WebhookPayloadInvalid, problem]));
        }

        if (!Uri.TryCreate(body.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !IntakeRules.IsHostAllowed(uri.Host, source.AllowedDomains))
        {
            await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, "Download domain not allowed", cancellationToken);

            return Response<ArtifactUploadDto>.Error(null, MessageKey.DownloadHostNotAllowed);
        }

        var sourceRef = new ArtifactSourceRef
        {
            Provider = body.Source?.Provider,
            RunId = body.Source?.RunId,
            RunUrl = body.Source?.RunUrl,
            Repository = body.Source?.Repository,
            Branch = body.Source?.Branch,
            Commit = body.Source?.Commit,
            Actor = body.Source?.Actor,
        };

        var result = await _intakeService.AcceptPulledAsync(new ArtifactPullRequest(source, Path.GetFileName(body.FileName), body.DownloadUrl, body.Size, body.Sha256.ToLowerInvariant(), ArtifactSource.Webhook, sourceRef), cancellationToken);

        await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, result.IsSuccess ? $"Session {result.Data?.SessionId}" : "Refused", cancellationToken);

        return result;
    }

    /// <summary>
    /// Body of a storeasy.artifact-ready/v1 webhook.
    /// </summary>
    public sealed class ArtifactReadyBody
    {
        /// <summary>
        /// Schema name.
        /// </summary>
        public string Schema { get; set; }

        /// <summary>
        /// App.
        /// </summary>
        public int AppId { get; set; }

        /// <summary>
        /// Platform (ios, android, windows).
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// File name.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Download address.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// SHA-256 of the file.
        /// </summary>
        public string Sha256 { get; set; }

        /// <summary>
        /// Size in bytes.
        /// </summary>
        public long? Size { get; set; }

        /// <summary>
        /// CI details.
        /// </summary>
        public ArtifactSourceRef Source { get; set; }
    }
}
