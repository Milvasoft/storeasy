using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Subscriptions.PlanSubscriptionChanges;

/// <summary>
/// Validations of <see cref="PlanSubscriptionChangesCommand"/>.
/// </summary>
public sealed class PlanSubscriptionChangesCommandValidator : AbstractValidator<PlanSubscriptionChangesCommand>
{
    ///<inheritdoc cref="PlanSubscriptionChangesCommandValidator"/>
    public PlanSubscriptionChangesCommandValidator(IMilvaLocalizer localizer)
    {
        this.PlanChangesRules(localizer);
    }
}
