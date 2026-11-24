using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.SendEasBuildToIntake;

/// <summary>
/// Handles <see cref="SendEasBuildToIntakeCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record SendEasBuildToIntakeCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<SendEasBuildToIntakeCommand, EasBuildDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasBuildDto>> Handle(SendEasBuildToIntakeCommand request, CancellationToken cancellationToken)
        => _easService.SendToIntakeAsync(request.OrganizationId, request.Id, cancellationToken);
}
