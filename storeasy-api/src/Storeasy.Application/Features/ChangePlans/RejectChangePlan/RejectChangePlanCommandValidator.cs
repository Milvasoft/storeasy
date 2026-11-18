using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.RejectChangePlan;

/// <summary>
/// Validations of <see cref="RejectChangePlanCommand"/>.
/// </summary>
public sealed class RejectChangePlanCommandValidator : AbstractValidator<RejectChangePlanCommand>
{
    ///<inheritdoc cref="RejectChangePlanCommandValidator"/>
    public RejectChangePlanCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.PlanId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlan]]);

        RuleFor(x => x.Reason)
            .Must(r => r != null && r.Trim().Length >= 10)
            .WithMessage(localizer[MessageKey.ReasonTooShort, 10]);
    }
}
