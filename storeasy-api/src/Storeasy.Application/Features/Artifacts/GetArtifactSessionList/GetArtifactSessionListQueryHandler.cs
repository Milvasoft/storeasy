using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Artifacts.GetArtifactSessionList;

/// <summary>
/// Handles <see cref="GetArtifactSessionListQuery"/>.
/// </summary>
/// <param name="SessionRepository"></param>
public record GetArtifactSessionListQueryHandler(IStoreasyRepositoryBase<ArtifactSession> SessionRepository) : IInterceptable, IListQueryHandler<GetArtifactSessionListQuery, ArtifactSessionListDto>
{
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = SessionRepository;

    /// <inheritdoc/>
    public Task<ListResponse<ArtifactSessionListDto>> Handle(GetArtifactSessionListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(ArtifactSessionListDto.Id), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        return _sessionRepository.GetAllAsync(request,
                                              condition: s => s.OrganizationId == request.OrganizationId
                                                              && (request.AppId == null || s.AppId == request.AppId)
                                                              && (request.Status == null || s.Status == request.Status)
                                                              && (request.Source == null || s.Source == request.Source),
                                              projection: ArtifactSessionListDto.Projection,
                                              cancellationToken: cancellationToken);
    }
}
