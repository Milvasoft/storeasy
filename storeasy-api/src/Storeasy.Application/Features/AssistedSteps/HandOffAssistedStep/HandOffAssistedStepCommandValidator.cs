using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.HandOffAssistedStep;

/// <summary>
/// Validations of <see cref="HandOffAssistedStepCommand"/>.
/// </summary>
public sealed class HandOffAssistedStepCommandValidator : AbstractValidator<HandOffAssistedStepCommand>
{
    ///<inheritdoc cref="HandOffAssistedStepCommandValidator"/>
    public HandOffAssistedStepCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StepId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAssistedStep]]);
    }
}
