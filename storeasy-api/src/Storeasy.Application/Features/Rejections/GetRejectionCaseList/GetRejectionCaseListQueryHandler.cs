using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.GetRejectionCaseList;

/// <summary>
/// Handles <see cref="GetRejectionCaseListQuery"/>.
/// </summary>
/// <param name="CaseRepository"></param>
public record GetRejectionCaseListQueryHandler(IStoreasyRepositoryBase<RejectionCase> CaseRepository) : IInterceptable, IListQueryHandler<GetRejectionCaseListQuery, RejectionCaseListDto>
{
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<RejectionCaseListDto>> Handle(GetRejectionCaseListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(RejectionCaseListDto.Id), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        var response = await _caseRepository.GetAllAsync(request,
                                                         condition: c => c.OrganizationId == request.OrganizationId
                                                                         && (request.AppId == null || c.AppId == request.AppId)
                                                                         && (request.State == null || c.State == request.State),
                                                         projection: RejectionCaseListDto.Projection,
                                                         cancellationToken: cancellationToken);

        response.Data?.ForEach(c => c.WithCounts());

        return response;
    }
}
