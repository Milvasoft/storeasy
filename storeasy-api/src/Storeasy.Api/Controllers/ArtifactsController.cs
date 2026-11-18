using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Features.Artifacts.CompleteArtifactSession;
using Storeasy.Application.Features.Artifacts.DeleteArtifactSession;
using Storeasy.Application.Features.Artifacts.DeliverArtifactSession;
using Storeasy.Application.Features.Artifacts.GetArtifactSessionDetail;
using Storeasy.Application.Features.Artifacts.GetArtifactSessionList;
using Storeasy.Application.Features.Artifacts.GetArtifactCounts;
using Storeasy.Application.Features.Artifacts.GetArtifactSummary;
using Storeasy.Application.Features.Artifacts.OpenArtifactSession;
using Storeasy.Application.Features.ArtifactSources.DeleteArtifactSource;
using Storeasy.Application.Features.ArtifactSources.GetArtifactSourceList;
using Storeasy.Application.Features.ArtifactSources.SaveArtifactSource;
using Storeasy.Application.Features.CiTokens.CreateCiToken;
using Storeasy.Application.Features.CiTokens.GetCiTokenList;
using Storeasy.Application.Features.CiTokens.RevokeCiToken;
using Storeasy.Application.Features.DeliveryRules.GetDeliveryRules;
using Storeasy.Application.Features.DeliveryRules.UpdateDeliveryRules;
using Storeasy.Application.Features.IntakeSettings.GetIntakeSettings;
using Storeasy.Application.Features.IntakeSettings.UpdateIntakeSettings;
using Storeasy.Application.Features.TrustedPublishers.CreateTrustedPublisher;
using Storeasy.Application.Features.TrustedPublishers.DeleteTrustedPublisher;
using Storeasy.Application.Features.TrustedPublishers.GetTrustedPublisherDetail;
using Storeasy.Application.Features.TrustedPublishers.GetTrustedPublisherList;
using Storeasy.Application.Features.TrustedPublishers.TestTrustedPublisher;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Package intake (docs/urun-plani.md 11.8, design 13, tools/CONTRACT.md 3): upload sessions with presigned multipart
/// uploads, CI tokens, trusted publishers, delivery rules, pull sources and settings. The upload endpoints accept CI
/// tokens and upload tokens for their apps; the organization of a token wins over any organization id in the request.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ArtifactsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Opens an upload session, resumes the open one of the same file, or reports a duplicate (ReleaseTest, CI and upload
    /// tokens of the app).
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("/" + GlobalConstant.RouteBase + "/apps/{appId:int}/artifacts")]
    [AllowMachineToken]
    [RateLimit(120, 3600, "artifact-open")]
    public Task<Response<ArtifactUploadDto>> OpenSessionAsync([FromRoute] int appId, [FromBody] OpenArtifactSessionCommand request, CancellationToken cancellation)
    {
        request.AppId = appId;

        return _mediator.Send(request, cancellation);
    }

    /// <summary>
    /// Completes the multipart upload with every part's ETag and starts processing. Idempotent.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("{sessionId:long}/complete")]
    [AllowMachineToken]
    public Task<Response<ArtifactSessionListDto>> CompleteSessionAsync([FromRoute] long sessionId, [FromBody] CompleteArtifactSessionCommand request, CancellationToken cancellation)
    {
        request.SessionId = sessionId;

        return _mediator.Send(request, cancellation);
    }

    /// <summary>
    /// Reads one session with its steps, package, checks and CI result.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("session")]
    [AllowMachineToken]
    [RateLimit(120, 60, "artifact-session")]
    public Task<Response<ArtifactSessionDetailDto>> GetSessionAsync([FromQuery] GetArtifactSessionDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Pages through the organization's sessions.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("sessions")]
    public Task<ListResponse<ArtifactSessionListDto>> GetSessionsAsync(GetArtifactSessionListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The last package of every app with its delivery rules.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("summary")]
    public Task<Response<List<ArtifactAppSummaryDto>>> GetSummaryAsync([FromQuery] GetArtifactSummaryQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Package counters of a period (default the last 30 days): total, per status and per source.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("counts")]
    public Task<Response<ArtifactCountsDto>> GetCountsAsync([FromQuery] GetArtifactCountsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Delivers a held session (ReleaseTest).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("session/deliver")]
    public Task<Response<ArtifactSessionDetailDto>> DeliverSessionAsync(DeliverArtifactSessionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Deletes the stored package of a finished session (ReleaseTest).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("session")]
    public Task<Response<long>> DeleteSessionAsync(DeleteArtifactSessionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the organization's CI tokens (values are never returned).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("ci-tokens")]
    public Task<Response<List<CiTokenDto>>> GetCiTokensAsync([FromQuery] GetCiTokenListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Creates a CI token (ManageConnections); the value is in this response only.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("ci-token")]
    [Idempotent]
    [WebSessionOnly]
    public Task<Response<CiTokenCreatedDto>> CreateCiTokenAsync(CreateCiTokenCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Revokes a CI token (ManageConnections).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("ci-token")]
    public Task<Response<int>> RevokeCiTokenAsync(RevokeCiTokenCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the trusted publisher rules.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("trusted-publishers")]
    public Task<Response<List<TrustedPublisherDto>>> GetTrustedPublishersAsync([FromQuery] GetTrustedPublisherListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one rule with the claims of its last exchange (ManageConnections).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("trusted-publisher")]
    public Task<Response<TrustedPublisherDto>> GetTrustedPublisherAsync([FromQuery] GetTrustedPublisherDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds a trusted publisher rule (ManageConnections).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("trusted-publisher")]
    [Idempotent]
    public Task<Response<int>> CreateTrustedPublisherAsync(CreateTrustedPublisherCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes a trusted publisher rule (ManageConnections).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("trusted-publisher")]
    public Task<Response<int>> DeleteTrustedPublisherAsync(DeleteTrustedPublisherCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Checks an OIDC token against a rule without issuing anything (ManageConnections).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("trusted-publisher/test")]
    [RateLimit(30, 600, "trusted-publisher-test")]
    public Task<Response<TrustedPublisherTestDto>> TestTrustedPublisherAsync(TestTrustedPublisherCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the delivery rules of an app.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("delivery-rules")]
    public Task<Response<DeliveryRulesDto>> GetDeliveryRulesAsync([FromQuery] GetDeliveryRulesQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Replaces the delivery rules of an app (ManageConnections). Test channels only.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("delivery-rules")]
    public Task<Response<int>> UpdateDeliveryRulesAsync(UpdateDeliveryRulesCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the pull sources (EAS, generic webhook).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("sources")]
    public Task<Response<List<ArtifactSourceDto>>> GetSourcesAsync([FromQuery] GetArtifactSourceListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Creates or updates a pull source (ManageConnections); the secret is returned on creation and rotation only.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("source")]
    [WebSessionOnly]
    public Task<Response<ArtifactSourceSavedDto>> SaveSourceAsync(SaveArtifactSourceCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes a pull source (ManageConnections).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("source")]
    public Task<Response<int>> DeleteSourceAsync(DeleteArtifactSourceCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the intake settings (retention, size limit).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("settings")]
    public Task<Response<IntakeSettingsDto>> GetSettingsAsync([FromQuery] GetIntakeSettingsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Changes the intake settings (ManageConnections).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("settings")]
    public Task<Response<int>> UpdateSettingsAsync(UpdateIntakeSettingsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
