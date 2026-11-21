using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoKeywords.ResearchAsoKeywords;

/// <summary>
/// Validations of <see cref="ResearchAsoKeywordsQuery"/>.
/// </summary>
public sealed class ResearchAsoKeywordsQueryValidator : AbstractValidator<ResearchAsoKeywordsQuery>
{
    ///<inheritdoc cref="ResearchAsoKeywordsQueryValidator"/>
    public ResearchAsoKeywordsQueryValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);
        this.AsoCountryRule(x => x.Country, localizer);
        this.AsoKeywordsRule(x => x.Keywords, ResearchAsoKeywordsQuery.MaxKeywords, localizer);
    }
}
