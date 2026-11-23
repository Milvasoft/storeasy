using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.RejectRegistryPlan;

/// <summary>
/// Validations of <see cref="RejectRegistryPlanCommand"/>.
/// </summary>
public sealed class RejectRegistryPlanCommandValidator : AbstractValidator<RejectRegistryPlanCommand>
{
    ///<inheritdoc cref="RejectRegistryPlanCommandValidator"/>
    public RejectRegistryPlanCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.PlanId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRegistryPlan]]);
    }
}
