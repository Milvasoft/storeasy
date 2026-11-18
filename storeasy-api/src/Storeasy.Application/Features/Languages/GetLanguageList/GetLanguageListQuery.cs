using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.LanguageDtos;

namespace Storeasy.Application.Features.Languages.GetLanguageList;

/// <summary>
/// Data transfer object for language list.
/// </summary>
public record GetLanguageListQuery : ListRequest, IListRequestQuery<LanguageDto>
{
}