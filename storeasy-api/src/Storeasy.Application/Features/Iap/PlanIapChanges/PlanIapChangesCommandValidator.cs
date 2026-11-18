using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Iap.PlanIapChanges;

/// <summary>
/// Validations of <see cref="PlanIapChangesCommand"/>.
/// </summary>
public sealed class PlanIapChangesCommandValidator : AbstractValidator<PlanIapChangesCommand>
{
    ///<inheritdoc cref="PlanIapChangesCommandValidator"/>
    public PlanIapChangesCommandValidator(IMilvaLocalizer localizer)
    {
        this.PlanChangesRules(localizer);
    }
}
