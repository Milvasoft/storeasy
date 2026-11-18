using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Account.GoogleSignIn;

/// <summary>
/// Validations of <see cref="GoogleSignInCommand"/>.
/// </summary>
public sealed class GoogleSignInCommandValidator : AbstractValidator<GoogleSignInCommand>
{
    ///<inheritdoc cref="GoogleSignInCommandValidator"/>
    public GoogleSignInCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Code)
            .NotNullOrEmpty(localizer, MessageKey.GlobalToken)
            .MaximumLength(2048)
            .WithMessage(localizer[MessageKey.GoogleSignInFailed]);

        RuleFor(command => command.State)
            .NotNullOrEmpty(localizer, MessageKey.GlobalToken)
            .MaximumLength(128)
            .WithMessage(localizer[MessageKey.GoogleSignInStateInvalid]);
    }
}
