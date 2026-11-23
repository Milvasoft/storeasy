using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.ApproveRegistryPlan;

/// <summary>
/// Validations of <see cref="ApproveRegistryPlanCommand"/>.
/// </summary>
public sealed class ApproveRegistryPlanCommandValidator : AbstractValidator<ApproveRegistryPlanCommand>
{
    ///<inheritdoc cref="ApproveRegistryPlanCommandValidator"/>
    public ApproveRegistryPlanCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.PlanId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRegistryPlan]]);
    }
}
