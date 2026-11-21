using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoExperiments.PlanAsoExperiment;

/// <summary>
/// Validations of <see cref="PlanAsoExperimentCommand"/>.
/// </summary>
public sealed class PlanAsoExperimentCommandValidator : AbstractValidator<PlanAsoExperimentCommand>
{
    ///<inheritdoc cref="PlanAsoExperimentCommandValidator"/>
    public PlanAsoExperimentCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);

        RuleFor(x => x.Operation)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);

        RuleFor(x => x.WinnerVariantKey)
            .MaximumLength(20)
            .WithMessage(localizer[MessageKey.AsoWinnerUnknown, string.Empty]);
    }
}
