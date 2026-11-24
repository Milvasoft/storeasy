using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.StartEasBuild;

/// <summary>
/// Handles <see cref="StartEasBuildCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record StartEasBuildCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<StartEasBuildCommand, List<EasBuildDto>>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<List<EasBuildDto>>> Handle(StartEasBuildCommand request, CancellationToken cancellationToken)
        => _easService.StartBuildAsync(request.OrganizationId, request.AppId, request.Platform, request.Profile, request.GitRef, request.Chain, request.Confirm, cancellationToken);
}
