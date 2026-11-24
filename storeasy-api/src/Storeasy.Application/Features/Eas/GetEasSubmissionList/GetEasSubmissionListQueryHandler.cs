using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasSubmissionList;

/// <summary>
/// Handles <see cref="GetEasSubmissionListQuery"/>.
/// </summary>
/// <param name="EasService"></param>
public record GetEasSubmissionListQueryHandler(IEasService EasService) : IInterceptable, IQueryHandler<GetEasSubmissionListQuery, List<EasSubmissionDto>>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<List<EasSubmissionDto>>> Handle(GetEasSubmissionListQuery request, CancellationToken cancellationToken)
        => _easService.ListSubmissionsAsync(request.OrganizationId, request.AppId, cancellationToken);
}
