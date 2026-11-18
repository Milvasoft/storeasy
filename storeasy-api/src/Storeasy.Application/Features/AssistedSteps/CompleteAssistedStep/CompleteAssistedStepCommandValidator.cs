using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.CompleteAssistedStep;

/// <summary>
/// Validations of <see cref="CompleteAssistedStepCommand"/>.
/// </summary>
public sealed class CompleteAssistedStepCommandValidator : AbstractValidator<CompleteAssistedStepCommand>
{
    ///<inheritdoc cref="CompleteAssistedStepCommandValidator"/>
    public CompleteAssistedStepCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StepId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAssistedStep]]);

        RuleFor(x => x.Status)
            .Must(s => s is AssistedStepStatus.InProgress or AssistedStepStatus.Done or AssistedStepStatus.Skipped)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAssistedStep]]);

        RuleFor(x => x.Note)
            .MaximumLength(Storeasy.Domain.Apps.AssistedStep.NoteMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalNote], Storeasy.Domain.Apps.AssistedStep.NoteMaxLength]);
    }
}
