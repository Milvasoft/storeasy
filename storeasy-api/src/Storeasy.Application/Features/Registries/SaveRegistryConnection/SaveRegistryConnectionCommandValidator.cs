using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.SaveRegistryConnection;

/// <summary>
/// Validations of <see cref="SaveRegistryConnectionCommand"/>.
/// </summary>
public sealed class SaveRegistryConnectionCommandValidator : AbstractValidator<SaveRegistryConnectionCommand>
{
    /// <summary>
    /// Longest secret accepted.
    /// </summary>
    public const int SecretMaxLength = 500;

    ///<inheritdoc cref="SaveRegistryConnectionCommandValidator"/>
    public SaveRegistryConnectionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Kind)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRegistryConnection]]);

        RuleFor(command => command.Secret)
            .Must(secret => !string.IsNullOrWhiteSpace(secret) && secret.Trim().Length is >= 8 and <= SecretMaxLength && !secret.Trim().Contains(' ', StringComparison.Ordinal))
            .WithMessage(localizer[MessageKey.RegistryConnectionSecretInvalid]);

        RuleFor(command => command.ExpiryDate)
            .Must(date => date == null || (date.Value.Year >= 2000 && date.Value.Year <= 2100))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRegistryConnection]]);
    }
}
