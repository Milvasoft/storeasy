using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Iap.ApplyIapPlan;

/// <summary>
/// Validations of <see cref="ApplyIapPlanCommand"/>.
/// </summary>
public sealed class ApplyIapPlanCommandValidator : AbstractValidator<ApplyIapPlanCommand>
{
    ///<inheritdoc cref="ApplyIapPlanCommandValidator"/>
    public ApplyIapPlanCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.PlanId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlan]]);
    }
}
