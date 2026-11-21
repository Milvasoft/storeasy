using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoExperiments.UpdateAsoExperimentChecklist;

/// <summary>
/// Validations of <see cref="UpdateAsoExperimentChecklistCommand"/>.
/// </summary>
public sealed class UpdateAsoExperimentChecklistCommandValidator : AbstractValidator<UpdateAsoExperimentChecklistCommand>
{
    ///<inheritdoc cref="UpdateAsoExperimentChecklistCommandValidator"/>
    public UpdateAsoExperimentChecklistCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);
    }
}
