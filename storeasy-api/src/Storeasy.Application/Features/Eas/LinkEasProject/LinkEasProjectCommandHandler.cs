using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.LinkEasProject;

/// <summary>
/// Handles <see cref="LinkEasProjectCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record LinkEasProjectCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<LinkEasProjectCommand, EasAppDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasAppDto>> Handle(LinkEasProjectCommand request, CancellationToken cancellationToken)
        => _easService.LinkProjectAsync(request.OrganizationId, request.AppId, request.ProjectId, cancellationToken);
}
