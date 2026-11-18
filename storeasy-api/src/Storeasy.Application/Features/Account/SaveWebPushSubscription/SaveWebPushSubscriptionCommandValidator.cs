using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Helpers;

namespace Storeasy.Application.Features.Account.SaveWebPushSubscription;

/// <summary>
/// Validations of <see cref="SaveWebPushSubscriptionCommand"/>.
/// </summary>
public sealed class SaveWebPushSubscriptionCommandValidator : AbstractValidator<SaveWebPushSubscriptionCommand>
{
    ///<inheritdoc cref="SaveWebPushSubscriptionCommandValidator"/>
    public SaveWebPushSubscriptionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Endpoint)
            .Must(endpoint => endpoint?.Length <= WebPushSubscription.EndpointMaxLength && WebPushKeys.IsEndpoint(endpoint))
            .WithMessage(localizer[MessageKey.WebPushSubscriptionInvalid]);

        RuleFor(command => command.P256dh)
            .Must(WebPushKeys.IsPublicKey)
            .WithMessage(localizer[MessageKey.WebPushSubscriptionInvalid]);

        RuleFor(command => command.Auth)
            .Must(WebPushKeys.IsAuthSecret)
            .WithMessage(localizer[MessageKey.WebPushSubscriptionInvalid]);
    }
}
