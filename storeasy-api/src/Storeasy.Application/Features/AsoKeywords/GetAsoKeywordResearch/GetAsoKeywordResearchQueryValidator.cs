using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordResearch;

/// <summary>
/// Validations of <see cref="GetAsoKeywordResearchQuery"/>.
/// </summary>
public sealed class GetAsoKeywordResearchQueryValidator : AbstractValidator<GetAsoKeywordResearchQuery>
{
    ///<inheritdoc cref="GetAsoKeywordResearchQueryValidator"/>
    public GetAsoKeywordResearchQueryValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);

        RuleFor(x => x.JobId)
            .Must(id => id == null || id > 0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreJob]]);
    }
}
