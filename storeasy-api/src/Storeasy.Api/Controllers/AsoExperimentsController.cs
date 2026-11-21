using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.AsoExperiments.DeleteAsoExperiment;
using Storeasy.Application.Features.AsoExperiments.GetAsoExperimentDetail;
using Storeasy.Application.Features.AsoExperiments.GetAsoExperimentExport;
using Storeasy.Application.Features.AsoExperiments.GetAsoExperimentOverview;
using Storeasy.Application.Features.AsoExperiments.PlanAsoExperiment;
using Storeasy.Application.Features.AsoExperiments.RecordAsoExperimentResult;
using Storeasy.Application.Features.AsoExperiments.SaveAsoExperiment;
using Storeasy.Application.Features.AsoExperiments.SyncAsoExperiments;
using Storeasy.Application.Features.AsoExperiments.UpdateAsoExperimentChecklist;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Store listing experiments and custom pages ("Deneyler"): App Store product page optimization and custom product pages
/// through the API (every store write a change plan), Play store listing experiments and custom store listings as an
/// assisted step with manual result entry; the Microsoft Store has none.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/aso-experiments")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AsoExperimentsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The records of an app and what each of its stores supports. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<AsoExperimentOverviewDto>> GetOverviewAsync([FromQuery] GetAsoExperimentOverviewQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// One record with its timeline and possible writes. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("experiment")]
    public Task<Response<AsoExperimentDto>> GetDetailAsync([FromQuery] GetAsoExperimentDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Creates or edits a draft record (nothing goes to a store). Editors and above.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("experiment")]
    public Task<Response<AsoExperimentDto>> SaveAsync(SaveAsoExperimentCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Deletes a record never written to a store, or a Play record. Editors and above.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("experiment")]
    public Task<Response<bool>> DeleteAsync([FromQuery] DeleteAsoExperimentCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans a store write of an App Store record (dry run by default). Editors make it; the approval needs the
    /// operation's permission and, except for writing a draft, the typed confirmation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("experiment/plan")]
    [RateLimit(60, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanAsync(PlanAsoExperimentCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Records the result (and, for Play, the state and dates) of a record. Editors and above.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("experiment/result")]
    public Task<Response<AsoExperimentDto>> RecordResultAsync(RecordAsoExperimentResultCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves the done items of a Play record's checklist. Editors and above.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("experiment/checklist")]
    public Task<Response<AsoExperimentDto>> UpdateChecklistAsync(UpdateAsoExperimentChecklistCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Download links of a record's files and its texts. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("experiment/export")]
    public Task<Response<AsoExperimentExportDto>> ExportAsync([FromQuery] GetAsoExperimentExportQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a read of an App Store record's experiments and custom product pages; returns the job id. Editors and above.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    public Task<Response<long>> SyncAsync(SyncAsoExperimentsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
