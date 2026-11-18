using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.ExchangeOidcToken;

/// <summary>
/// Handles <see cref="ExchangeOidcTokenCommand"/>. Not logged: the request and the response carry tokens.
/// </summary>
/// <param name="ExchangeService"></param>
public record ExchangeOidcTokenCommandHandler(IOidcExchangeService ExchangeService) : IInterceptable, ICommandHandler<ExchangeOidcTokenCommand, OidcExchangeDto>
{
    private readonly IOidcExchangeService _exchangeService = ExchangeService;

    /// <inheritdoc/>
    public Task<Response<OidcExchangeDto>> Handle(ExchangeOidcTokenCommand request, CancellationToken cancellationToken)
        => _exchangeService.ExchangeAsync(request.AppId, request.Token, cancellationToken);
}
