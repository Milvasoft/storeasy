using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoKeywords.UntrackAsoKeywords;

/// <summary>
/// Validations of <see cref="UntrackAsoKeywordsCommand"/>.
/// </summary>
public sealed class UntrackAsoKeywordsCommandValidator : AbstractValidator<UntrackAsoKeywordsCommand>
{
    ///<inheritdoc cref="UntrackAsoKeywordsCommandValidator"/>
    public UntrackAsoKeywordsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Ids)
            .Must(ids => ids is { Count: > 0 } && ids.Count <= UntrackAsoKeywordsCommand.MaxIds && ids.All(id => id > 0))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoTrackedKeyword]]);
    }
}
