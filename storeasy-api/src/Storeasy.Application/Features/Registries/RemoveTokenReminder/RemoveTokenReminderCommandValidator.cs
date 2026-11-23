using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.RemoveTokenReminder;

/// <summary>
/// Validations of <see cref="RemoveTokenReminderCommand"/>.
/// </summary>
public sealed class RemoveTokenReminderCommandValidator : AbstractValidator<RemoveTokenReminderCommand>
{
    ///<inheritdoc cref="RemoveTokenReminderCommandValidator"/>
    public RemoveTokenReminderCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTokenReminder]]);
    }
}
