using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.Listings.GetCategories;
using Storeasy.Application.Features.Listings.PlanCategories;
using Storeasy.Application.Features.Listings.SuggestCategories;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// App Store categories, copyright and content rights declaration.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Reads the categories, copyright and content rights live, with the categories the store offers.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<CategoryDto>> GetCategoriesAsync([FromQuery] GetCategoriesQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans categories, copyright and content rights (DryRun false approves at once).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanCategoriesAsync(PlanCategoriesCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Suggests the App Store primary and secondary category with the organization's AI key (editors and above), from the
    /// store's own list. Nothing is saved or sent: the member reviews it and plans it. 409 AiKeyNotSet without a key.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("suggest")]
    [RateLimit(120, 3600, "ai-category-suggest")]
    public Task<Response<CategorySuggestionDto>> SuggestAsync(SuggestCategoriesCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
