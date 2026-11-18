using FluentValidation;

namespace Storeasy.Application.Features.Languages.GetLanguageList;

/// <summary>
/// Validations of <see cref="GetLanguageListQuery"/>.
/// </summary>
public sealed class GetLanguageListQueryValidator : AbstractValidator<GetLanguageListQuery>
{
    ///<inheritdoc cref="GetLanguageListQueryValidator"/>
    public GetLanguageListQueryValidator()
    {
    }
}