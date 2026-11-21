using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Application.Features.StoreConnections.AddStoreConnection;

namespace Storeasy.Application.Features.StoreConnections.ReplaceStoreConnectionKey;

/// <summary>
/// Validations of <see cref="ReplaceStoreConnectionKeyCommand"/>. The secret's shape depends on the connection's store, so
/// the handler parses it; here it only has to be present and not too long.
/// </summary>
public sealed class ReplaceStoreConnectionKeyCommandValidator : AbstractValidator<ReplaceStoreConnectionKeyCommand>
{
    ///<inheritdoc cref="ReplaceStoreConnectionKeyCommandValidator"/>
    public ReplaceStoreConnectionKeyCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.Secret)
            .NotNullOrEmpty(localizer, MessageKey.StoreSecret)
            .MaximumLength(AddStoreConnectionCommandValidator.SecretMaxLength)
            .WithMessage(localizer[MessageKey.StoreSecret]);
    }
}
