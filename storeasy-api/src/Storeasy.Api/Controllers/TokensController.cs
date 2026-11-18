using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.TokenDtos;
using Storeasy.Application.Features.Tokens.CreatePersonalAccessToken;
using Storeasy.Application.Features.Tokens.GetCurrentToken;
using Storeasy.Application.Features.Tokens.GetPersonalAccessTokenList;
using Storeasy.Application.Features.Tokens.RevokePersonalAccessToken;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Personal access tokens (sea_) for the REST API and the remote MCP (design 16.11-16.14), and the description of the
/// token of the request (tools/CONTRACT.md 2.1). Tokens are created and revoked from a web session only.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class TokensController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lists the caller's personal access tokens of every organization.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<List<PersonalAccessTokenDto>>> GetTokensAsync(CancellationToken cancellation) => _mediator.Send(new GetPersonalAccessTokenListQuery(), cancellation);

    /// <summary>
    /// Creates a personal access token; the value is in this response only.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("token")]
    [Idempotent]
    [WebSessionOnly]
    [RateLimit(30, 3600, "token-create")]
    public Task<Response<PersonalAccessTokenCreatedDto>> CreateTokenAsync(CreatePersonalAccessTokenCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Revokes one of the caller's personal access tokens.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("token")]
    [WebSessionOnly]
    public Task<Response<int>> RevokeTokenAsync(RevokePersonalAccessTokenCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Describes the token of the request (personal access, CI or upload token). Never returns the value.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("current")]
    [AllowMachineToken]
    public Task<Response<CurrentTokenDto>> GetCurrentAsync(CancellationToken cancellation) => _mediator.Send(new GetCurrentTokenQuery(), cancellation);
}
