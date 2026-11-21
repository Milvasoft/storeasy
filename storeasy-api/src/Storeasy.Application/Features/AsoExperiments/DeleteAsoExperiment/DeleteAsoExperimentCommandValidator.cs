using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoExperiments.DeleteAsoExperiment;

/// <summary>
/// Validations of <see cref="DeleteAsoExperimentCommand"/>.
/// </summary>
public sealed class DeleteAsoExperimentCommandValidator : AbstractValidator<DeleteAsoExperimentCommand>
{
    ///<inheritdoc cref="DeleteAsoExperimentCommandValidator"/>
    public DeleteAsoExperimentCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);
    }
}
