using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Intake;
using System.Text;
using System.Text.Json;

namespace Storeasy.Application.Features.ArtifactSources.ReceiveEasWebhook;

/// <summary>
/// Handles <see cref="ReceiveEasWebhookCommand"/>.
/// </summary>
/// <param name="SourceRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="IntakeService"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
public record ReceiveEasWebhookCommandHandler(IStoreasyRepositoryBase<ArtifactPullSource> SourceRepository,
                                              ISecretProtector SecretProtector,
                                              IArtifactIntakeService IntakeService,
                                              IMilvaLocalizer Localizer,
                                              TimeProvider TimeProvider) : IInterceptable, ICommandHandler<ReceiveEasWebhookCommand, ArtifactUploadDto>
{
    private readonly IStoreasyRepositoryBase<ArtifactPullSource> _sourceRepository = SourceRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IArtifactIntakeService _intakeService = IntakeService;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<ArtifactUploadDto>> Handle(ReceiveEasWebhookCommand request, CancellationToken cancellationToken)
    {
        var source = await _sourceRepository.GetFirstOrDefaultAsync(s => s.PublicId == request.SourceId && s.Kind == ArtifactPullSourceKind.Eas,
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

        if (!PullWebhooks.IsValidEasSignature(secret, request.RawBody, request.Signature))
        {
            await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, "Invalid signature", cancellationToken);

            return Response<ArtifactUploadDto>.Error(null, MessageKey.WebhookSignatureInvalid);
        }

        JsonElement root;

        try
        {
            root = JsonDocument.Parse(request.RawBody).RootElement.Clone();
        }
        catch (JsonException)
        {
            await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, "Body is not JSON", cancellationToken);

            return Response<ArtifactUploadDto>.Error(null, new ResponseMessage(MessageKey.WebhookPayloadInvalid, _localizer[MessageKey.WebhookPayloadInvalid, "JSON"]));
        }

        var status = Str(root, "status");
        var platform = Str(root, "platform");
        var buildUrl = Str(Get(root, "artifacts"), "buildUrl");
        var metadata = Get(root, "metadata");
        var distribution = Str(metadata, "distribution");
        var artifactPlatform = platform == "ios" ? ArtifactPlatform.Ios : platform == "android" ? ArtifactPlatform.Android : (ArtifactPlatform?)null;

        if (status != "finished" || buildUrl == null || artifactPlatform == null || distribution == "simulator" || !source.Platforms.Contains(artifactPlatform.Value))
        {
            await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, $"Ignored ({status ?? "no status"}, {platform ?? "no platform"})", cancellationToken);

            return Response<ArtifactUploadDto>.Success(null);
        }

        if (!Uri.TryCreate(buildUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !IntakeRules.IsHostAllowed(uri.Host, source.AllowedDomains))
        {
            await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, "Download domain not allowed", cancellationToken);

            return Response<ArtifactUploadDto>.Error(null, MessageKey.DownloadHostNotAllowed);
        }

        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();

        if (extension is not (".ipa" or ".aab" or ".apk") || (extension == ".apk" && !source.IncludeApk))
        {
            await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, $"Ignored ({extension} build)", cancellationToken);

            return Response<ArtifactUploadDto>.Success(null);
        }

        var fileName = $"{Str(metadata, "appName") ?? "build"}-{Str(metadata, "appVersion") ?? "0"}-{Str(metadata, "appBuildVersion") ?? Str(root, "id")}{extension}";

        var sourceRef = new ArtifactSourceRef
        {
            Provider = "eas",
            RunId = Str(root, "id"),
            RunUrl = Str(root, "buildDetailsPageUrl"),
            Commit = Str(metadata, "gitCommitHash"),
            Branch = Str(metadata, "channel") ?? Str(metadata, "buildProfile"),
            Actor = Str(metadata, "username"),
        };

        var result = await _intakeService.AcceptPulledAsync(new ArtifactPullRequest(source, fileName, buildUrl, null, null, ArtifactSource.Eas, sourceRef), cancellationToken);

        await _sourceRepository.RecordPullCallAsync(source.Id, _timeProvider.GetUtcNow().UtcDateTime, result.IsSuccess ? $"Session {result.Data?.SessionId}" : "Refused", cancellationToken);

        return result;
    }

    private static JsonElement Get(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string Str(JsonElement element, string name) => Get(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : Get(element, name) is { ValueKind: JsonValueKind.Number } number ? number.GetRawText() : null;
}
