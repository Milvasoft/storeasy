using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordSeeds;

/// <summary>
/// Validations of <see cref="GetAsoKeywordSeedsQuery"/>.
/// </summary>
public sealed class GetAsoKeywordSeedsQueryValidator : AbstractValidator<GetAsoKeywordSeedsQuery>
{
    ///<inheritdoc cref="GetAsoKeywordSeedsQueryValidator"/>
    public GetAsoKeywordSeedsQueryValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);
        this.AsoCountryRule(x => x.Country, localizer);
    }
}
