using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.GetMockupStarterTemplates;

/// <summary>
/// Handles <see cref="GetMockupStarterTemplatesQuery"/>.
/// </summary>
/// <param name="starterTemplateCatalog"></param>
public class GetMockupStarterTemplatesQueryHandler(IStarterTemplateCatalog starterTemplateCatalog) : IInterceptable, IQueryHandler<GetMockupStarterTemplatesQuery, List<MockupStarterTemplateDto>>
{
    private readonly IStarterTemplateCatalog _starterTemplateCatalog = starterTemplateCatalog;

    /// <inheritdoc/>
    public Task<Response<List<MockupStarterTemplateDto>>> Handle(GetMockupStarterTemplatesQuery request, CancellationToken cancellationToken)
    {
        var starters = _starterTemplateCatalog.All.Select(s => new MockupStarterTemplateDto
        {
            Key = s.Key,
            Name = s.NameEn,
            NameTr = s.NameTr,
            Description = s.DescriptionEn,
            DescriptionTr = s.DescriptionTr,
            SlotCount = s.SlotCount,
            SampleLanguages = [.. s.Captions.Keys.Order()],
            Spec = s.Spec,
        }).ToList();

        return Task.FromResult(Response<List<MockupStarterTemplateDto>>.Success(starters));
    }
}
