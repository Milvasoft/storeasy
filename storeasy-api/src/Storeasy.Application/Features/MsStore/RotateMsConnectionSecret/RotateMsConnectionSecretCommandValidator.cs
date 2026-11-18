using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Application.Features.StoreConnections.AddStoreConnection;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.RotateMsConnectionSecret;

/// <summary>
/// Validations of <see cref="RotateMsConnectionSecretCommand"/>.
/// </summary>
public sealed class RotateMsConnectionSecretCommandValidator : AbstractValidator<RotateMsConnectionSecretCommand>
{
    ///<inheritdoc cref="RotateMsConnectionSecretCommandValidator"/>
    public RotateMsConnectionSecretCommandValidator(IMilvaLocalizer localizer, TimeProvider timeProvider)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.Secret)
            .NotNullOrEmpty(localizer, MessageKey.StoreSecret)
            .MaximumLength(AddStoreConnectionCommandValidator.SecretMaxLength)
            .WithMessage(localizer[MessageKey.StoreSecret]);

        RuleFor(command => command.CredentialExpiryDate)
            .Must(date => MsSecretRules.IsValidExpiry(date, timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage(localizer[MessageKey.MsSecretExpiryInvalid]);
    }
}
