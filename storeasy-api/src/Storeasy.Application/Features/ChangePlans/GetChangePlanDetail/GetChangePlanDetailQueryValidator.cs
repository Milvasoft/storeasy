using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.GetChangePlanDetail;

/// <summary>
/// Validations of <see cref="GetChangePlanDetailQuery"/>.
/// </summary>
public sealed class GetChangePlanDetailQueryValidator : AbstractValidator<GetChangePlanDetailQuery>
{
    ///<inheritdoc cref="GetChangePlanDetailQueryValidator"/>
    public GetChangePlanDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.PlanId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlan]]);
    }
}
