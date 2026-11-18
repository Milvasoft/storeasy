using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.DeliveryRules.UpdateDeliveryRules;

/// <summary>
/// Validations of <see cref="UpdateDeliveryRulesCommand"/>.
/// </summary>
public sealed class UpdateDeliveryRulesCommandValidator : AbstractValidator<UpdateDeliveryRulesCommand>
{
    ///<inheritdoc cref="UpdateDeliveryRulesCommandValidator"/>
    public UpdateDeliveryRulesCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.Rules)
            .Must(rules => rules != null && rules.Select(r => r.Platform).Distinct().Count() == rules.Count)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlatform]]);

        RuleForEach(x => x.Rules)
            .Must(r => Enum.IsDefined(r.Platform) && Enum.IsDefined(r.Target) && Enum.IsDefined(r.OnWarning) && (r.TargetId?.Length ?? 0) <= 200 && (r.TargetName?.Length ?? 0) <= 200
                       && (r.RolloutFraction == null || (r.RolloutFraction > 0 && r.RolloutFraction <= 1)))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalDeliveryTarget]]);
    }
}
