using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Features.RuleWatcher.ApproveRuleProposal;
using Storeasy.Application.Features.RuleWatcher.FetchRuleSources;
using Storeasy.Application.Features.RuleWatcher.GetRuleProposalDetail;
using Storeasy.Application.Features.RuleWatcher.GetRuleProposalList;
using Storeasy.Application.Features.RuleWatcher.GetRuleSourceList;
using Storeasy.Application.Features.RuleWatcher.PrepareRuleProposal;
using Storeasy.Application.Features.RuleWatcher.RejectRuleProposal;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// The rule watcher of the platform admins (docs/urun-plani.md 12.3): watched sources and catalog proposals. The catalog
/// itself (<c>rules</c>, <c>rules/rule</c>, <c>rules/catalog</c>) belongs to the readiness phase.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/rules")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
[UserTypeAuth(UserType.Manager)]
public class RuleWatcherController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The watched sources and their last read.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("sources")]
    [Auth(PermissionCatalog.RuleCatalogManagement.List)]
    public Task<Response<List<RuleSourceDto>>> GetSourcesAsync(CancellationToken cancellation) => _mediator.Send(new GetRuleSourceListQuery(), cancellation);

    /// <summary>
    /// Reads every source now.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sources/fetch")]
    [Auth(PermissionCatalog.RuleCatalogManagement.Approve)]
    [RateLimit(10, 3600, "rules-fetch")]
    public Task<Response<RuleWatchResultDto>> FetchSourcesAsync(CancellationToken cancellation) => _mediator.Send(new FetchRuleSourcesCommand(), cancellation);

    /// <summary>
    /// Pages through the catalog proposals.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("proposals")]
    [Auth(PermissionCatalog.RuleCatalogManagement.List)]
    public Task<ListResponse<RuleProposalListDto>> GetProposalsAsync(GetRuleProposalListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one proposal with its diff and impact.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("proposals/proposal")]
    [Auth(PermissionCatalog.RuleCatalogManagement.List)]
    public Task<Response<RuleProposalDetailDto>> GetProposalAsync([FromQuery] GetRuleProposalDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Writes the catalog side of a proposal (the rules it changes, the patch and a note); the proposal is then shown as
    /// prepared by Claude. Platform admin only, so it is not an MCP tool (MCP tools are organization scoped).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("proposal/prepare")]
    [Auth(PermissionCatalog.RuleCatalogManagement.Approve)]
    public Task<Response<int>> PrepareProposalAsync(PrepareRuleProposalCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Approves a proposal and notifies the affected apps' owners and admins.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("proposal/approve")]
    [Auth(PermissionCatalog.RuleCatalogManagement.Approve)]
    [Idempotent]
    public Task<Response<RuleProposalDecisionDto>> ApproveProposalAsync(ApproveRuleProposalCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Rejects a proposal.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("proposal/reject")]
    [Auth(PermissionCatalog.RuleCatalogManagement.Approve)]
    public Task<Response<int>> RejectProposalAsync(RejectRuleProposalCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
