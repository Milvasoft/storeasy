using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.GetMockupTemplateList;

/// <summary>
/// Handles <see cref="GetMockupTemplateListQuery"/>. Newest change first by default.
/// </summary>
/// <param name="templateRepository"></param>
public class GetMockupTemplateListQueryHandler(IStoreasyRepositoryBase<MockupTemplate> templateRepository) : IInterceptable, IListQueryHandler<GetMockupTemplateListQuery, MockupTemplateListDto>
{
    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = templateRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<MockupTemplateListDto>> Handle(GetMockupTemplateListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(50);
        request.Sorting ??= new SortRequest { SortBy = nameof(MockupTemplateListDto.Id), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        var response = await _templateRepository.GetAllAsync(request,
                                                             condition: t => t.OrganizationId == request.OrganizationId && (request.AppId == null || t.AppId == request.AppId),
                                                             projection: MockupTemplateListDto.Projection,
                                                             cancellationToken: cancellationToken);

        return response;
    }
}
