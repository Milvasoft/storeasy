using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.LanguageDtos;

namespace Storeasy.Application.Features.Languages.GetLanguageList;

/// <summary>
/// Handles the language list operation.
/// </summary>
/// <param name="languageRepository"></param>
public class GetLanguageListQueryHandler(IStoreasyRepositoryBase<Language> languageRepository) : IInterceptable, IListQueryHandler<GetLanguageListQuery, LanguageDto>
{
    private readonly IStoreasyRepositoryBase<Language> _languageRepository = languageRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<LanguageDto>> Handle(GetLanguageListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);

        var response = await _languageRepository.GetAllAsync(request, projection: LanguageDto.Projection, cancellationToken: cancellationToken);

        return response;
    }
}
