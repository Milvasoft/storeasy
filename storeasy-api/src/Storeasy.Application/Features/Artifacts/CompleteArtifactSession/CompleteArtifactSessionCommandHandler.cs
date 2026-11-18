using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.CompleteArtifactSession;

/// <summary>
/// Handles <see cref="CompleteArtifactSessionCommand"/>.
/// </summary>
/// <param name="IntakeService"></param>
public record CompleteArtifactSessionCommandHandler(IArtifactIntakeService IntakeService) : IInterceptable, ICommandHandler<CompleteArtifactSessionCommand, ArtifactSessionListDto>
{
    private readonly IArtifactIntakeService _intakeService = IntakeService;

    /// <inheritdoc/>
    public Task<Response<ArtifactSessionListDto>> Handle(CompleteArtifactSessionCommand request, CancellationToken cancellationToken)
        => _intakeService.CompleteAsync(request.OrganizationId, request.SessionId, [.. (request.Parts ?? []).Select(p => new UploadedPart(p.PartNumber, p.ETag))], cancellationToken);
}
