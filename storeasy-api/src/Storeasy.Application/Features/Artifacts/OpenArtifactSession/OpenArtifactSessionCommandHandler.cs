using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.OpenArtifactSession;

/// <summary>
/// Handles <see cref="OpenArtifactSessionCommand"/>.
/// </summary>
/// <param name="IntakeService"></param>
public record OpenArtifactSessionCommandHandler(IArtifactIntakeService IntakeService) : IInterceptable, ICommandHandler<OpenArtifactSessionCommand, ArtifactUploadDto>
{
    private readonly IArtifactIntakeService _intakeService = IntakeService;

    /// <inheritdoc/>
    public Task<Response<ArtifactUploadDto>> Handle(OpenArtifactSessionCommand request, CancellationToken cancellationToken)
        => _intakeService.OpenAsync(new ArtifactOpenRequest(request.OrganizationId,
                                                            request.AppId,
                                                            request.FileName,
                                                            request.Size,
                                                            request.Sha256,
                                                            request.ContentType,
                                                            request.Source,
                                                            request.SourceRef,
                                                            request.Delivery),
                                    cancellationToken);
}
