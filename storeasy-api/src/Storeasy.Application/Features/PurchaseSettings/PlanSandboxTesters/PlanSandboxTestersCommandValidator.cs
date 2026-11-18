using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.PurchaseSettings.PlanSandboxTesters;

/// <summary>
/// Validations of <see cref="PlanSandboxTestersCommand"/>.
/// </summary>
public sealed class PlanSandboxTestersCommandValidator : AbstractValidator<PlanSandboxTestersCommand>
{
    ///<inheritdoc cref="PlanSandboxTestersCommandValidator"/>
    public PlanSandboxTestersCommandValidator(IMilvaLocalizer localizer)
    {
        this.PlanChangesRules(localizer);
    }
}
