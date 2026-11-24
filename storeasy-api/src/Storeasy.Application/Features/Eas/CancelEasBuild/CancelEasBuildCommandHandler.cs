using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.CancelEasBuild;

/// <summary>
/// Handles <see cref="CancelEasBuildCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record CancelEasBuildCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<CancelEasBuildCommand, EasBuildDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasBuildDto>> Handle(CancelEasBuildCommand request, CancellationToken cancellationToken)
        => _easService.CancelBuildAsync(request.OrganizationId, request.Id, cancellationToken);
}
