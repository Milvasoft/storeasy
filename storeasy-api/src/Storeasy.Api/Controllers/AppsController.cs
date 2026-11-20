using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AnswerDtos;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Features.Answers.CopySavedAnswers;
using Storeasy.Application.Features.Answers.GetSavedAnswers;
using Storeasy.Application.Features.Answers.PlanDataSafety;
using Storeasy.Application.Features.Answers.UpdateSavedAnswers;
using Storeasy.Application.Features.Apps.UpdateAppProfile;
using Storeasy.Application.Features.Readiness.GetAppCompliance;
using Storeasy.Application.Features.Readiness.GetAppsOverview;
using Storeasy.Application.Features.Readiness.GetReadiness;
using Storeasy.Application.Features.Apps.AddGoogleApp;
using Storeasy.Application.Features.Apps.GetAppDetail;
using Storeasy.Application.Features.Apps.GetAppList;
using Storeasy.Application.Features.Apps.LinkStoreApp;
using Storeasy.Application.Features.Apps.ReorderApps;
using Storeasy.Application.Features.Apps.SetAppAlertsMuted;
using Storeasy.Application.Features.Apps.SetAppHidden;
using Storeasy.Application.Features.Apps.SyncApps;
using Storeasy.Application.Features.Apps.UnlinkStoreApp;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// App endpoints: storeasy apps and their records in each store (App Store Connect, Google Play, later Microsoft Store),
/// with their builds. Store data arrives through sync jobs; nothing here calls a store in the request.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AppsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Pages through the organization's apps with their store records (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<ListResponse<AppListDto>> GetAppsAsync(GetAppListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one app with its store records and newest builds (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("app")]
    public Task<Response<AppDetailDto>> GetAppAsync([FromQuery] GetAppDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds a Google Play app by package name (editors and above). A sync job confirms it and reads its builds; follow the
    /// returned job.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("google")]
    [RateLimit(60, 3600, "apps-add-google")]
    public Task<Response<AppAddedDto>> AddGoogleAppAsync(AddGoogleAppCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Moves a store record to another app (editors and above); at most one record per store in an app.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("app/link")]
    public Task<Response<int>> LinkStoreAppAsync(LinkStoreAppCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Splits a store record off into its own app (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("app/unlink")]
    public Task<Response<int>> UnlinkStoreAppAsync(UnlinkStoreAppCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Hides an app from the apps list and the organization wide overviews, or shows it again (editors and above). The
    /// app stays reachable by its id; sync never shows it again.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("app/hidden")]
    public Task<Response<int>> SetAppHiddenAsync(SetAppHiddenCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Mutes an app's alerts or turns them on again (editors and above). The app stays in the apps list and on its own
    /// pages and keeps syncing; the live activity board and the notifications to members leave it out.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("app/alerts-muted")]
    public Task<Response<int>> SetAppAlertsMutedAsync(SetAppAlertsMutedCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sets the order of the apps list (editors and above): the listed apps come first in that order, every other app
    /// follows by name. Returns the number of placed apps.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("app/order")]
    public Task<Response<int>> ReorderAppsAsync(ReorderAppsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a sync of apps and builds for one connection or all (editors and above). Returns the job ids.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(30, 3600, "apps-sync")]
    public Task<Response<List<long>>> SyncAppsAsync(SyncAppsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sets the app's catalog profile: device families, regions, features, build SDK, target API level (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("profile")]
    public Task<Response<int>> UpdateProfileAsync(UpdateAppProfileCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// What is missing before a submission of the app in one store, read live (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("readiness")]
    [RateLimit(240, 3600, "apps-readiness")]
    public Task<Response<ReadinessReportDto>> GetReadinessAsync([FromQuery] GetReadinessQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Every app of the organization with each store's last readiness check and the next deadline; Refresh checks live
    /// first (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("overview")]
    [RateLimit(60, 3600, "apps-overview")]
    public Task<Response<List<AppOverviewDto>>> GetOverviewAsync([FromQuery] GetAppsOverviewQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The app's compliance timeline: catalog deadlines that apply to it, with their state (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("compliance")]
    public Task<Response<List<ComplianceItemDto>>> GetComplianceAsync([FromQuery] GetAppComplianceQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The app's saved answer documents (any member; the demo password is never returned).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("answers")]
    public Task<Response<SavedAnswerSetDto>> GetAnswersAsync([FromQuery] GetSavedAnswersQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Writes one saved answer document (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("answers")]
    public Task<Response<int>> UpdateAnswersAsync(UpdateSavedAnswersCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Copies saved answer documents from another app (editors and above; never the demo password).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("answers/copy")]
    public Task<Response<int>> CopyAnswersAsync(CopySavedAnswersCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans sending the Play Data safety form generated from the saved answers (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("answers/data-safety")]
    [RateLimit(60, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanDataSafetyAsync(PlanDataSafetyCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
