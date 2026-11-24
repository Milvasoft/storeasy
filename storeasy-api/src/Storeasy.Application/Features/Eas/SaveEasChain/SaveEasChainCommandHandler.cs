using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.SaveEasChain;

/// <summary>
/// Handles <see cref="SaveEasChainCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record SaveEasChainCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<SaveEasChainCommand, EasAppDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasAppDto>> Handle(SaveEasChainCommand request, CancellationToken cancellationToken)
        => _easService.SaveChainAsync(request.OrganizationId, request.AppId, request.Chain, cancellationToken);
}
