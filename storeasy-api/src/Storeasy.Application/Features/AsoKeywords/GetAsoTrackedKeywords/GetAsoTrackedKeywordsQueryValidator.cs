using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Aso;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoTrackedKeywords;

/// <summary>
/// Validations of <see cref="GetAsoTrackedKeywordsQuery"/>.
/// </summary>
public sealed class GetAsoTrackedKeywordsQueryValidator : AbstractValidator<GetAsoTrackedKeywordsQuery>
{
    ///<inheritdoc cref="GetAsoTrackedKeywordsQueryValidator"/>
    public GetAsoTrackedKeywordsQueryValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);

        RuleFor(x => x.Country)
            .Must(c => string.IsNullOrWhiteSpace(c) || AsoCountries.Normalize(c) != null)
            .WithMessage(localizer[MessageKey.AsoCountryInvalid]);
    }
}
