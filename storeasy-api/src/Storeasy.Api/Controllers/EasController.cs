using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.EasDtos;
using Storeasy.Application.Features.Eas.CancelEasBuild;
using Storeasy.Application.Features.Eas.GetEasApp;
using Storeasy.Application.Features.Eas.GetEasBuild;
using Storeasy.Application.Features.Eas.GetEasProfiles;
using Storeasy.Application.Features.Eas.GetEasSubmission;
using Storeasy.Application.Features.Eas.GetEasSubmissionList;
using Storeasy.Application.Features.Eas.SaveEasChain;
using Storeasy.Application.Features.Eas.StartEasSubmission;
using Storeasy.Application.Features.Eas.GetEasProjectList;
using Storeasy.Application.Features.Eas.GetExpoConnection;
using Storeasy.Application.Features.Eas.LinkEasProject;
using Storeasy.Application.Features.Eas.RemoveExpoConnection;
using Storeasy.Application.Features.Eas.SaveExpoConnection;
using Storeasy.Application.Features.Eas.SendEasBuildToIntake;
using Storeasy.Application.Features.Eas.StartEasBuild;
using Storeasy.Application.Features.Eas.SyncEasBuilds;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain;

namespace Storeasy.Api.Controllers;

/// <summary>
/// EAS builds through the Expo API (docs/eas-builds.md): the organization's Expo token, EAS project links, builds started
/// from GitHub on a member's request, their progress, and handing a finished package to the build intake. The whole
/// controller answers 503 while the <c>EasBuilds</c> feature flag is off.
/// </summary>
/// <param name="mediator"></param>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
[FeatureFlag(FeatureFlagKeys.EasBuilds)]
public class EasController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The organization's Expo connection (the actor's name, never the token).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("connection")]
    public Task<Response<ExpoConnectionDto>> GetConnectionAsync([FromQuery] GetExpoConnectionQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves or replaces the Expo access token after Expo named its actor.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("connection")]
    [RateLimit(30, 3600, "eas-connection-save")]
    public Task<Response<ExpoConnectionDto>> SaveConnectionAsync(SaveExpoConnectionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes the Expo access token.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("connection")]
    public Task<Response<int>> RemoveConnectionAsync(RemoveExpoConnectionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The EAS projects the token can see, best matches of an app first.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("projects")]
    [RateLimit(120, 3600, "eas-projects")]
    public Task<Response<List<EasProjectDto>>> GetProjectsAsync([FromQuery] GetEasProjectListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Links an app to an EAS project, or unlinks it.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("project")]
    public Task<Response<EasAppDto>> LinkProjectAsync(LinkEasProjectCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// An app's EAS project link and recent builds.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("app")]
    public Task<Response<EasAppDto>> GetAppAsync([FromQuery] GetEasAppQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The build profiles of the linked repository, or the defaults.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("profiles")]
    [RateLimit(120, 3600, "eas-profiles")]
    public Task<Response<EasProfilesDto>> GetProfilesAsync([FromQuery] GetEasProfilesQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Starts EAS builds from GitHub after the typed confirmation (uses the EAS build quota).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("build/start")]
    [RateLimit(30, 3600, "eas-build-start")]
    public Task<Response<List<EasBuildDto>>> StartBuildAsync(StartEasBuildCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// One EAS build.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("build")]
    public Task<Response<EasBuildDto>> GetBuildAsync([FromQuery] GetEasBuildQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Cancels a queued or running EAS build.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("build/cancel")]
    public Task<Response<EasBuildDto>> CancelBuildAsync(CancelEasBuildCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Hands a finished build's package to the build intake.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("build/intake")]
    [RateLimit(60, 3600, "eas-build-intake")]
    public Task<Response<EasBuildDto>> SendToIntakeAsync(SendEasBuildToIntakeCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a read of the recent EAS builds of an app, or of every linked app.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(60, 3600, "eas-sync")]
    public Task<Response<long>> SyncAsync(SyncEasBuildsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves the app's after build defaults (send to intake, EAS Submit, review plan).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("chain")]
    public Task<Response<EasAppDto>> SaveChainAsync(SaveEasChainCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Starts EAS Submit of a finished build after the typed confirmation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("submission/start")]
    [RateLimit(30, 3600, "eas-submit-start")]
    public Task<Response<EasSubmissionDto>> StartSubmissionAsync(StartEasSubmissionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The EAS Submit submissions of an app.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("submissions")]
    public Task<Response<List<EasSubmissionDto>>> GetSubmissionsAsync([FromQuery] GetEasSubmissionListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// One EAS Submit submission.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("submission")]
    public Task<Response<EasSubmissionDto>> GetSubmissionAsync([FromQuery] GetEasSubmissionQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
