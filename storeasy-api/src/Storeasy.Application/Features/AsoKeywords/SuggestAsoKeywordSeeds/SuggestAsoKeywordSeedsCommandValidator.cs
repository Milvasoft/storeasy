using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoKeywords.SuggestAsoKeywordSeeds;

/// <summary>
/// Validations of <see cref="SuggestAsoKeywordSeedsCommand"/>.
/// </summary>
public sealed class SuggestAsoKeywordSeedsCommandValidator : AbstractValidator<SuggestAsoKeywordSeedsCommand>
{
    ///<inheritdoc cref="SuggestAsoKeywordSeedsCommandValidator"/>
    public SuggestAsoKeywordSeedsCommandValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);
        this.AsoCountryRule(x => x.Country, localizer);

        RuleFor(x => x.Seeds)
            .Must(s => s == null || s.Count <= SuggestAsoKeywordSeedsCommand.MaxSeeds)
            .WithMessage(localizer[MessageKey.AsoKeywordsInvalid, SuggestAsoKeywordSeedsCommand.MaxSeeds, Domain.Aso.AsoTrackedKeyword.KeywordMaxLength]);

        RuleFor(x => x.Instruction)
            .MaximumLength(SuggestAsoKeywordSeedsCommand.InstructionMaxLength)
            .WithMessage(localizer[MessageKey.AiSuggestionNoteTooLong, SuggestAsoKeywordSeedsCommand.InstructionMaxLength]);
    }
}
