using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Features.Rejections.CloseRejectionCase;
using Storeasy.Application.Features.Rejections.GetRejectionCaseDetail;
using Storeasy.Application.Features.Rejections.GetRejectionCaseList;
using Storeasy.Application.Features.Rejections.GetRejectionHistory;
using Storeasy.Application.Features.Rejections.MarkRejectionReplySent;
using Storeasy.Application.Features.Rejections.OpenRejectionCase;
using Storeasy.Application.Features.Rejections.UpdateRejectionChecklist;
using Storeasy.Application.Features.Rejections.UpdateRejectionReplyDraft;
using Storeasy.Application.Features.Rejections.UpdateRejectionText;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Rejection cases (docs/urun-plani.md 11.3): opened by the App Store Connect webhook or the version state sync, the
/// review text pasted, the guidelines turned into a checklist, the Resolution Center reply drafted and marked as sent.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class RejectionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Pages through the rejection cases.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<ListResponse<RejectionCaseListDto>> GetCasesAsync(GetRejectionCaseListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one case.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("case")]
    public Task<Response<RejectionCaseDto>> GetCaseAsync([FromQuery] GetRejectionCaseDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Opens a case by hand.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("case")]
    [Idempotent]
    public Task<Response<int>> OpenCaseAsync(OpenRejectionCaseCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves the pasted review text and builds the checklist from its guidelines.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("case/text")]
    public Task<Response<RejectionCaseDto>> UpdateTextAsync(UpdateRejectionTextCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Replaces the checklist.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("case/checklist")]
    public Task<Response<RejectionCaseDto>> UpdateChecklistAsync(UpdateRejectionChecklistCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves the Resolution Center reply draft (4,000 character limit shown, not enforced on save).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("case/reply-draft")]
    public Task<Response<RejectionReplyDraftDto>> UpdateReplyDraftAsync(UpdateRejectionReplyDraftCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Records that the reply was sent in the Resolution Center (assisted step; no API sends it).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("case/reply-sent")]
    public Task<Response<RejectionReplyDraftDto>> MarkReplySentAsync(MarkRejectionReplySentCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Closes a case by hand.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("case/close")]
    public Task<Response<int>> CloseCaseAsync(CloseRejectionCaseCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The submission history of an app with its rejections.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("history")]
    public Task<Response<RejectionHistoryDto>> GetHistoryAsync([FromQuery] GetRejectionHistoryQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
