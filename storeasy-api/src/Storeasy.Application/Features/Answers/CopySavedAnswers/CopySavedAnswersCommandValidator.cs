using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.CopySavedAnswers;

/// <summary>
/// Validations of <see cref="CopySavedAnswersCommand"/>.
/// </summary>
public sealed class CopySavedAnswersCommandValidator : AbstractValidator<CopySavedAnswersCommand>
{
    ///<inheritdoc cref="CopySavedAnswersCommandValidator"/>
    public CopySavedAnswersCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.SourceAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.TargetAppId)
            .GreaterThan(0)
            .NotEqual(x => x.SourceAppId)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleForEach(x => x.Keys)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAnswers]]);
    }
}
