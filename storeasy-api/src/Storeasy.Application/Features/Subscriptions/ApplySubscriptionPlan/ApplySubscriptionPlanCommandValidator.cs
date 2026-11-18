using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Subscriptions.ApplySubscriptionPlan;

/// <summary>
/// Validations of <see cref="ApplySubscriptionPlanCommand"/>.
/// </summary>
public sealed class ApplySubscriptionPlanCommandValidator : AbstractValidator<ApplySubscriptionPlanCommand>
{
    ///<inheritdoc cref="ApplySubscriptionPlanCommandValidator"/>
    public ApplySubscriptionPlanCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.PlanId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlan]]);
    }
}
