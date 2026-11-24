using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.SaveExpoConnection;

/// <summary>
/// Handles <see cref="SaveExpoConnectionCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record SaveExpoConnectionCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<SaveExpoConnectionCommand, ExpoConnectionDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<ExpoConnectionDto>> Handle(SaveExpoConnectionCommand request, CancellationToken cancellationToken)
        => _easService.SaveConnectionAsync(request.OrganizationId, request.Token, cancellationToken);
}
