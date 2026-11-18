using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.ApplyChangePlan;

/// <summary>
/// Validations of <see cref="ApplyChangePlanCommand"/>.
/// </summary>
public sealed class ApplyChangePlanCommandValidator : AbstractValidator<ApplyChangePlanCommand>
{
    ///<inheritdoc cref="ApplyChangePlanCommandValidator"/>
    public ApplyChangePlanCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.PlanId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlan]]);
    }
}
