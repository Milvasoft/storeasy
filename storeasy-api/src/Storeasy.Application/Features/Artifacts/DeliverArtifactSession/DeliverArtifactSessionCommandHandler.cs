using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.DeliverArtifactSession;

/// <summary>
/// Handles <see cref="DeliverArtifactSessionCommand"/>.
/// </summary>
/// <param name="IntakeService"></param>
[Log]
public record DeliverArtifactSessionCommandHandler(IArtifactIntakeService IntakeService) : IInterceptable, ICommandHandler<DeliverArtifactSessionCommand, ArtifactSessionDetailDto>
{
    private readonly IArtifactIntakeService _intakeService = IntakeService;

    /// <inheritdoc/>
    public Task<Response<ArtifactSessionDetailDto>> Handle(DeliverArtifactSessionCommand request, CancellationToken cancellationToken)
        => _intakeService.DeliverHeldAsync(request.OrganizationId, request.SessionId, cancellationToken);
}
