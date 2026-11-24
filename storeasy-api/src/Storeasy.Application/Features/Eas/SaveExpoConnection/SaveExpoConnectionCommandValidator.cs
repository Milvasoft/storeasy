using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Eas.SaveExpoConnection;

/// <summary>
/// Validations of <see cref="SaveExpoConnectionCommand"/>.
/// </summary>
public sealed class SaveExpoConnectionCommandValidator : AbstractValidator<SaveExpoConnectionCommand>
{
    /// <summary>
    /// Longest token accepted.
    /// </summary>
    public const int TokenMaxLength = 500;

    ///<inheritdoc cref="SaveExpoConnectionCommandValidator"/>
    public SaveExpoConnectionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token) && token.Trim().Length is >= 8 and <= TokenMaxLength && !token.Trim().Any(char.IsWhiteSpace))
            .WithMessage(localizer[MessageKey.ExpoTokenInvalid]);
    }
}
