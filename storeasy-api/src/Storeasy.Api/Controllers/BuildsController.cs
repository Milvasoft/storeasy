using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.Builds.GetBuildList;
using Storeasy.Application.Features.Builds.PlanBuildLink;
using Storeasy.Application.Features.Builds.PlanBundleUpload;
using Storeasy.Application.Features.Builds.PlanExportCompliance;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Build endpoints: synced builds of a store record, attaching a build to an App Store version, export compliance and Play bundle uploads, all as change plans.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class BuildsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Pages through the synced builds of a store record, newest first.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<ListResponse<BuildDto>> GetBuildsAsync(GetBuildListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans attaching a VALID build (and its export compliance answer) to the App Store version.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("link")]
    [RateLimit(240, 3600, "content-plan")]
    [Idempotent]
    public Task<Response<ChangePlanDto>> PlanBuildLinkAsync(PlanBuildLinkCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans the export compliance answer of the attached build.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("export-compliance")]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanExportComplianceAsync(PlanExportComplianceCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans uploading an Android App Bundle (an uploaded asset) and putting it on a track.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("bundle")]
    [RateLimit(240, 3600, "content-plan")]
    [Idempotent]
    public Task<Response<ChangePlanDto>> PlanBundleAsync(PlanBundleUploadCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
