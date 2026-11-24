using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasProjectList;

/// <summary>
/// Handles <see cref="GetEasProjectListQuery"/>.
/// </summary>
/// <param name="EasService"></param>
public record GetEasProjectListQueryHandler(IEasService EasService) : IInterceptable, IQueryHandler<GetEasProjectListQuery, List<EasProjectDto>>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<List<EasProjectDto>>> Handle(GetEasProjectListQuery request, CancellationToken cancellationToken)
        => _easService.ListProjectsAsync(request.OrganizationId, request.AppId, cancellationToken);
}
