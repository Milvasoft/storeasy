using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Features.AsoKeywords.GetAsoKeywordCoverage;
using Storeasy.Application.Features.AsoKeywords.GetAsoKeywordRanks;
using Storeasy.Application.Features.AsoKeywords.GetAsoKeywordResearch;
using Storeasy.Application.Features.AsoKeywords.GetAsoKeywordSeeds;
using Storeasy.Application.Features.AsoKeywords.GetAsoTrackedKeywords;
using Storeasy.Application.Features.AsoKeywords.ResearchAsoKeywords;
using Storeasy.Application.Features.AsoKeywords.SuggestAsoKeywordSeeds;
using Storeasy.Application.Features.AsoKeywords.TrackAsoKeywords;
using Storeasy.Application.Features.AsoKeywords.UntrackAsoKeywords;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// ASO keywords: research from the stores' public data (estimated popularity, difficulty, competitors, current rank), keyword
/// candidates, daily rank tracking with history, and Apple's keywords field helper. Reads need View; tracking changes and AI
/// ideas need EditContent.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/aso-keywords")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AsoKeywordsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The tracked keywords of an app in one store with current rank, 1, 7 and 30 day change and the last 30 days.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<List<AsoTrackedKeywordDto>>> GetTrackedAsync([FromQuery] GetAsoTrackedKeywordsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues the measurement of up to 20 keywords in one store and country (estimated popularity, difficulty, competing
    /// apps, top ten, current rank and opportunity) as a store job and answers its id at once. Store calls are paced, so a
    /// research of many new keywords takes minutes; read it with GET research.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("research")]
    [RateLimit(60, 3600, "aso-research")]
    public Task<Response<long>> ResearchAsync(ResearchAsoKeywordsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// A research job of an app in one store: its state, keywords measured so far and, once it succeeded, the measurements.
    /// Without a job id, the app's latest research in that store (no data when there was none).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("research")]
    public Task<Response<AsoKeywordResearchJobDto>> GetResearchAsync([FromQuery] GetAsoKeywordResearchQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Keyword candidates from data: the listing, competitors' names and the store's suggestions.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("seeds")]
    [RateLimit(60, 3600, "aso-seeds")]
    public Task<Response<AsoKeywordSeedsDto>> GetSeedsAsync([FromQuery] GetAsoKeywordSeedsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Keyword ideas grouped by theme from the organization's AI (editors and above). Candidates only: research scores
    /// them from data. 409 AiKeyNotSet without a key.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("seeds/suggest")]
    [RateLimit(60, 3600, "ai-aso-seeds")]
    public Task<Response<AsoKeywordSeedsDto>> SuggestSeedsAsync(SuggestAsoKeywordSeedsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Starts or resumes the daily rank tracking of keywords (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("track")]
    public Task<Response<List<AsoTrackedKeywordDto>>> TrackAsync(TrackAsoKeywordsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Stops the daily rank tracking of keywords; their history is kept (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("untrack")]
    public Task<Response<int>> UntrackAsync(UntrackAsoKeywordsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The daily rank history of one tracked keyword with its followed competitors and the latest top ten.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("ranks")]
    public Task<Response<AsoKeywordRanksDto>> GetRanksAsync([FromQuery] GetAsoKeywordRanksQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Apple's keywords field helper per listing language: covered and missing tracked keywords, repeated words and the
    /// characters left of 100.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("coverage")]
    public Task<Response<List<AsoKeywordCoverageDto>>> GetCoverageAsync([FromQuery] GetAsoKeywordCoverageQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
