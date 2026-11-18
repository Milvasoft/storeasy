using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Helpers;

namespace Storeasy.Application.Features.Notifications.UpdateNotificationPreference;

/// <summary>
/// Validations of <see cref="UpdateNotificationPreferenceCommand"/>.
/// </summary>
public sealed class UpdateNotificationPreferenceCommandValidator : AbstractValidator<UpdateNotificationPreferenceCommand>
{
    ///<inheritdoc cref="UpdateNotificationPreferenceCommandValidator"/>
    public UpdateNotificationPreferenceCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Kind)
            .Must(kind => NotificationKinds.Configurable.Contains(kind))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalNotificationType]]);
    }
}
