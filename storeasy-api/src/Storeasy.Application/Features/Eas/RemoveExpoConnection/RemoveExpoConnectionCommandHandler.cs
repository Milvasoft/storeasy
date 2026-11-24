using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.RemoveExpoConnection;

/// <summary>
/// Handles <see cref="RemoveExpoConnectionCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record RemoveExpoConnectionCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<RemoveExpoConnectionCommand, int>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<int>> Handle(RemoveExpoConnectionCommand request, CancellationToken cancellationToken)
        => _easService.RemoveConnectionAsync(request.OrganizationId, cancellationToken);
}
