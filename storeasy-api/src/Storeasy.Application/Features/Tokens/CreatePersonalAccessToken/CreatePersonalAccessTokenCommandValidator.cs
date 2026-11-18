using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Tokens;

namespace Storeasy.Application.Features.Tokens.CreatePersonalAccessToken;

/// <summary>
/// Validations of <see cref="CreatePersonalAccessTokenCommand"/>.
/// </summary>
public sealed class CreatePersonalAccessTokenCommandValidator : AbstractValidator<CreatePersonalAccessTokenCommand>
{
    /// <summary>
    /// Lifetimes a personal access token may have.
    /// </summary>
    public static readonly int[] AllowedExpiryDays = [30, 90, 365];

    ///<inheritdoc cref="CreatePersonalAccessTokenCommandValidator"/>
    public CreatePersonalAccessTokenCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= PersonalAccessToken.NameMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(x => x.ExpiryDays)
            .Must(days => AllowedExpiryDays.Contains(days))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalExpiryDays]]);
    }
}
