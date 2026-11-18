using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.GetMockupTemplateDetail;

/// <summary>
/// Handles <see cref="GetMockupTemplateDetailQuery"/>. A template of another organization is reported as not found.
/// </summary>
/// <param name="templateRepository"></param>
public class GetMockupTemplateDetailQueryHandler(IStoreasyRepositoryBase<MockupTemplate> templateRepository) : IInterceptable, IQueryHandler<GetMockupTemplateDetailQuery, MockupTemplateDetailDto>
{
    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = templateRepository;

    /// <inheritdoc/>
    public async Task<Response<MockupTemplateDetailDto>> Handle(GetMockupTemplateDetailQuery request, CancellationToken cancellationToken)
    {
        var template = await _templateRepository.GetFirstOrDefaultAsync(t => t.Id == request.TemplateId && t.OrganizationId == request.OrganizationId,
                                                                        projection: MockupTemplateDetailDto.Projection,
                                                                        cancellationToken: cancellationToken);

        if (template == null)
            return Response<MockupTemplateDetailDto>.Error(null, MessageKey.MockupTemplateNotFound);

        template.Spec = MockupSpecRules.Upgrade(template.Spec);

        return Response<MockupTemplateDetailDto>.Success(template);
    }
}
