using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoKeywords.ResearchAsoKeywords;

/// <summary>
/// Validations of <see cref="ResearchAsoKeywordsCommand"/>.
/// </summary>
public sealed class ResearchAsoKeywordsCommandValidator : AbstractValidator<ResearchAsoKeywordsCommand>
{
    ///<inheritdoc cref="ResearchAsoKeywordsCommandValidator"/>
    public ResearchAsoKeywordsCommandValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);
        this.AsoCountryRule(x => x.Country, localizer);
        this.AsoKeywordsRule(x => x.Keywords, ResearchAsoKeywordsCommand.MaxKeywords, localizer);
    }
}
