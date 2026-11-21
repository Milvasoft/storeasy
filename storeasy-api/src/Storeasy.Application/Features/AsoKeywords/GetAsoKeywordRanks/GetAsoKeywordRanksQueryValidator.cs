using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordRanks;

/// <summary>
/// Validations of <see cref="GetAsoKeywordRanksQuery"/>.
/// </summary>
public sealed class GetAsoKeywordRanksQueryValidator : AbstractValidator<GetAsoKeywordRanksQuery>
{
    ///<inheritdoc cref="GetAsoKeywordRanksQueryValidator"/>
    public GetAsoKeywordRanksQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoTrackedKeyword]]);

        RuleFor(x => x.Days)
            .InclusiveBetween(1, GetAsoKeywordRanksQuery.MaxDays)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, "Days"]);
    }
}
