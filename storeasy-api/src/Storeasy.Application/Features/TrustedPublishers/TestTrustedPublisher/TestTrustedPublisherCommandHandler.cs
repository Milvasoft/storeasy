using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.TrustedPublishers.TestTrustedPublisher;

/// <summary>
/// Handles <see cref="TestTrustedPublisherCommand"/>.
/// </summary>
/// <param name="ExchangeService"></param>
public record TestTrustedPublisherCommandHandler(IOidcExchangeService ExchangeService) : IInterceptable, ICommandHandler<TestTrustedPublisherCommand, TrustedPublisherTestDto>
{
    private readonly IOidcExchangeService _exchangeService = ExchangeService;

    /// <inheritdoc/>
    public Task<Response<TrustedPublisherTestDto>> Handle(TestTrustedPublisherCommand request, CancellationToken cancellationToken)
        => _exchangeService.TestAsync(request.OrganizationId, request.Id, request.Token, cancellationToken);
}
