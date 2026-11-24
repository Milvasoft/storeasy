using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.StartEasSubmission;

/// <summary>
/// Handles <see cref="StartEasSubmissionCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record StartEasSubmissionCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<StartEasSubmissionCommand, EasSubmissionDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasSubmissionDto>> Handle(StartEasSubmissionCommand request, CancellationToken cancellationToken)
        => _easService.StartSubmissionAsync(request.OrganizationId, request.Id, request.Track, request.ReleaseStatus, request.Confirm, cancellationToken);
}
