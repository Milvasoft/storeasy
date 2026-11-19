using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AiSettings.SetAiKey;

/// <summary>
/// Validations of <see cref="SetAiKeyCommand"/>: the shape of an Anthropic API key, never its value in a message.
/// </summary>
public sealed class SetAiKeyCommandValidator : AbstractValidator<SetAiKeyCommand>
{
    /// <summary>
    /// Prefix of every Anthropic API key.
    /// </summary>
    public const string KeyPrefix = "sk-ant-";

    /// <summary>
    /// Longest key accepted.
    /// </summary>
    public const int KeyMaxLength = 300;

    ///<inheritdoc cref="SetAiKeyCommandValidator"/>
    public SetAiKeyCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ApiKey)
            .Must(BeAnAnthropicKey)
            .WithMessage(localizer[MessageKey.AiKeyInvalid]);
    }

    private static bool BeAnAnthropicKey(string apiKey)
    {
        var key = apiKey?.Trim();

        return !string.IsNullOrEmpty(key)
               && key.Length > KeyPrefix.Length + 4
               && key.Length <= KeyMaxLength
               && key.StartsWith(KeyPrefix, StringComparison.Ordinal)
               && key.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }
}
