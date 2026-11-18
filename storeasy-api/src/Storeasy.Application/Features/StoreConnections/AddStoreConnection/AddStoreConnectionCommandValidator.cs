using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreConnections.AddStoreConnection;

/// <summary>
/// Validations of <see cref="AddStoreConnectionCommand"/>.
/// </summary>
public sealed class AddStoreConnectionCommandValidator : AbstractValidator<AddStoreConnectionCommand>
{
    /// <summary>
    /// Upper bound of a pasted secret (a service account JSON is about 2.3 KB).
    /// </summary>
    public const int SecretMaxLength = 16 * 1024;

    ///<inheritdoc cref="AddStoreConnectionCommandValidator"/>
    public AddStoreConnectionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Store)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.StoreInvalid]);

        RuleFor(command => command.Name)
            .NotNullOrEmpty(localizer, MessageKey.GlobalName)
            .MaximumLength(StoreConnection.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], StoreConnection.NameMaxLength]);

        RuleFor(command => command.Secret)
            .NotNullOrEmpty(localizer, MessageKey.StoreSecret)
            .MaximumLength(SecretMaxLength)
            .WithMessage(localizer[MessageKey.StoreSecret]);
    }
}
