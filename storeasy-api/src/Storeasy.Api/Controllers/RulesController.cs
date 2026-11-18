using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Features.Rules.GetRuleCatalog;
using Storeasy.Application.Features.Rules.GetRuleDetail;
using Storeasy.Application.Features.Rules.GetRuleList;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Store rules catalog endpoints (docs/urun-plani.md 12): the loaded catalog's version, its rules with the organization's matching apps. Read only; any member.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class RulesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Version of the loaded rules catalog.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("catalog")]
    public Task<Response<RuleCatalogInfoDto>> GetCatalogAsync([FromQuery] GetRuleCatalogQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the catalog rules with the number of the organization's apps each applies to.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<Response<List<RuleListDto>>> GetRulesAsync(GetRuleListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one rule with its sources and the organization's matching apps.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("rule")]
    public Task<Response<RuleDetailDto>> GetRuleAsync([FromQuery] GetRuleDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
