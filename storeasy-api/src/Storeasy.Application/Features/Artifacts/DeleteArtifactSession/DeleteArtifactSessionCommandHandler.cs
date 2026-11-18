using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Artifacts.DeleteArtifactSession;

/// <summary>
/// Handles <see cref="DeleteArtifactSessionCommand"/>.
/// </summary>
/// <param name="IntakeService"></param>
[Log]
public record DeleteArtifactSessionCommandHandler(IArtifactIntakeService IntakeService) : IInterceptable, ICommandHandler<DeleteArtifactSessionCommand, long>
{
    private readonly IArtifactIntakeService _intakeService = IntakeService;

    /// <inheritdoc/>
    public Task<Response<long>> Handle(DeleteArtifactSessionCommand request, CancellationToken cancellationToken)
        => _intakeService.DeletePackageAsync(request.OrganizationId, request.SessionId, cancellationToken);
}
