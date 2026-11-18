using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.UpdateSavedAnswers;

/// <summary>
/// Validations of <see cref="UpdateSavedAnswersCommand"/>.
/// </summary>
public sealed class UpdateSavedAnswersCommandValidator : AbstractValidator<UpdateSavedAnswersCommand>
{
    ///<inheritdoc cref="UpdateSavedAnswersCommandValidator"/>
    public UpdateSavedAnswersCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.Key)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAnswers]]);

        RuleFor(x => x.DemoPassword)
            .Empty()
            .When(x => x.Key != SavedAnswerKind.ReviewLogin)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPassword]]);
    }
}
