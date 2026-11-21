using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Features.AsoKeywords.TrackAsoKeywords;

/// <summary>
/// Validations of <see cref="TrackAsoKeywordsCommand"/>.
/// </summary>
public sealed class TrackAsoKeywordsCommandValidator : AbstractValidator<TrackAsoKeywordsCommand>
{
    ///<inheritdoc cref="TrackAsoKeywordsCommandValidator"/>
    public TrackAsoKeywordsCommandValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);
        this.AsoCountryRule(x => x.Country, localizer);
        this.AsoKeywordsRule(x => x.Keywords, TrackAsoKeywordsCommand.MaxKeywords, localizer);

        RuleFor(x => x.CompetitorIds)
            .Must(ids => ids == null || (ids.Count <= AsoTrackedKeyword.MaxCompetitors && ids.All(id => !string.IsNullOrWhiteSpace(id) && id.Trim().Length <= AsoCompetitorRank.CompetitorIdMaxLength)))
            .WithMessage(localizer[MessageKey.AsoCompetitorsInvalid, AsoTrackedKeyword.MaxCompetitors]);
    }
}
