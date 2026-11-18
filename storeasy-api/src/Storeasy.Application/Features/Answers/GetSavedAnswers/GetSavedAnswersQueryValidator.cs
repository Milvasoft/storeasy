using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AnswerDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.GetSavedAnswers;

/// <summary>
/// Validations of <see cref="GetSavedAnswersQuery"/>.
/// </summary>
public sealed class GetSavedAnswersQueryValidator : AbstractValidator<GetSavedAnswersQuery>
{
    ///<inheritdoc cref="GetSavedAnswersQueryValidator"/>
    public GetSavedAnswersQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
