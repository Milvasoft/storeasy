using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Features.Artifacts.ExchangeOidcToken;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Trusted publishing (tools/CONTRACT.md 3.1): a CI run exchanges its provider's OIDC token for a 15 minute upload token.
/// Anonymous; the OIDC token is the credential.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class OidcController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Exchanges an OIDC token for an upload token: 401 OidcTokenInvalid, 403 OidcClaimMismatch, 404 AppNotFound.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("exchange")]
    [AllowAnonymous]
    [RateLimit(60, 60, "oidc-exchange")]
    public Task<Response<OidcExchangeDto>> ExchangeAsync(ExchangeOidcTokenCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
