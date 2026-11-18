using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.DiscardChangePlan;

/// <summary>
/// Validations of <see cref="DiscardChangePlanCommand"/>.
/// </summary>
public sealed class DiscardChangePlanCommandValidator : AbstractValidator<DiscardChangePlanCommand>
{
    ///<inheritdoc cref="DiscardChangePlanCommandValidator"/>
    public DiscardChangePlanCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.PlanId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlan]]);
    }
}
