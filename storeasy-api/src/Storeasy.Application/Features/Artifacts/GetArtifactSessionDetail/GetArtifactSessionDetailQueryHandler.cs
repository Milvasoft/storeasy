using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.GetArtifactSessionDetail;

/// <summary>
/// Handles <see cref="GetArtifactSessionDetailQuery"/>.
/// </summary>
/// <param name="IntakeService"></param>
public record GetArtifactSessionDetailQueryHandler(IArtifactIntakeService IntakeService) : IInterceptable, IQueryHandler<GetArtifactSessionDetailQuery, ArtifactSessionDetailDto>
{
    private readonly IArtifactIntakeService _intakeService = IntakeService;

    /// <inheritdoc/>
    public async Task<Response<ArtifactSessionDetailDto>> Handle(GetArtifactSessionDetailQuery request, CancellationToken cancellationToken)
    {
        var session = await _intakeService.GetAsync(request.OrganizationId, request.SessionId, cancellationToken);

        return session == null ? Response<ArtifactSessionDetailDto>.Error(null, MessageKey.ArtifactSessionNotFound) : Response<ArtifactSessionDetailDto>.Success(session);
    }
}
