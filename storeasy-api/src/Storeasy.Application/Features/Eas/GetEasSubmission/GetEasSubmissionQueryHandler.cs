using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasSubmission;

/// <summary>
/// Handles <see cref="GetEasSubmissionQuery"/>.
/// </summary>
/// <param name="EasService"></param>
public record GetEasSubmissionQueryHandler(IEasService EasService) : IInterceptable, IQueryHandler<GetEasSubmissionQuery, EasSubmissionDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasSubmissionDto>> Handle(GetEasSubmissionQuery request, CancellationToken cancellationToken)
        => _easService.GetSubmissionAsync(request.OrganizationId, request.Id, cancellationToken);
}
