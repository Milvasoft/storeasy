using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Chrome.PlanChromeChange;

/// <summary>
/// Validations of <see cref="PlanChromeChangeCommand"/>. The rules that depend on the item's status are checked by the
/// planner, which reports every problem at once.
/// </summary>
public sealed class PlanChromeChangeCommandValidator : AbstractValidator<PlanChromeChangeCommand>
{
    ///<inheritdoc cref="PlanChromeChangeCommandValidator"/>
    public PlanChromeChangeCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(command => command.Kind)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanChromeChangeCommand.Kind)]);

        RuleFor(command => command.SessionId)
            .GreaterThan(0)
            .When(command => command.SessionId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanChromeChangeCommand.SessionId)]);

        RuleFor(command => command.DeployPercentage)
            .InclusiveBetween(0, 100)
            .When(command => command.DeployPercentage.HasValue)
            .WithMessage(command => localizer[MessageKey.ChromePercentageInvalid, command.DeployPercentage]);

        RuleFor(command => command.TargetVersion)
            .MaximumLength(30)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanChromeChangeCommand.TargetVersion)]);

        RuleFor(command => command.NewVersion)
            .MaximumLength(30)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanChromeChangeCommand.NewVersion)]);

        RuleFor(command => command.Reason)
            .MaximumLength(1000)
            .WithMessage(localizer[MessageKey.MaxLengthReached, nameof(PlanChromeChangeCommand.Reason), 1000]);
    }
}
