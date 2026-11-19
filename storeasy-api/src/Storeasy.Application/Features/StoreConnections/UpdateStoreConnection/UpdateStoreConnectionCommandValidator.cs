using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreConnections.UpdateStoreConnection;

/// <summary>
/// Validations of <see cref="UpdateStoreConnectionCommand"/>. Whether a store logo fits the connection's store needs the
/// row, so the handler checks that; here the icon only has to be one of the keys.
/// </summary>
public sealed class UpdateStoreConnectionCommandValidator : AbstractValidator<UpdateStoreConnectionCommand>
{
    ///<inheritdoc cref="UpdateStoreConnectionCommandValidator"/>
    public UpdateStoreConnectionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.Name)
            .NotNullOrEmpty(localizer, MessageKey.GlobalName)
            .MaximumLength(StoreConnection.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], StoreConnection.NameMaxLength]);

        RuleFor(command => command.Icon)
            .Must(icon => StoreConnectionIcons.IsKnown(StoreConnectionIcons.Normalize(icon)))
            .WithMessage(localizer[MessageKey.StoreConnectionIconInvalid]);
    }
}
