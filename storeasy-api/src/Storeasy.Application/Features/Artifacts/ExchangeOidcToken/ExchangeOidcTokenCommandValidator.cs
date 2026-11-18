using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Artifacts.ExchangeOidcToken;

/// <summary>
/// Validations of <see cref="ExchangeOidcTokenCommand"/>.
/// </summary>
public sealed class ExchangeOidcTokenCommandValidator : AbstractValidator<ExchangeOidcTokenCommand>
{
    ///<inheritdoc cref="ExchangeOidcTokenCommandValidator"/>
    public ExchangeOidcTokenCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.ValidationError]);

        RuleFor(x => x.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token) && token.Length <= 16384)
            .WithMessage(localizer[MessageKey.ValidationError]);
    }
}
