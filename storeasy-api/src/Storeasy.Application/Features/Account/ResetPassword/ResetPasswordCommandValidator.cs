using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Identity.Abstract;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Account.ResetPassword;

/// <summary>
/// Validations of <see cref="ResetPasswordCommand"/>. The password policy is checked before the token is used, so a
/// weak password does not burn the link.
/// </summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    ///<inheritdoc cref="ResetPasswordCommandValidator"/>
    public ResetPasswordCommandValidator(IMilvaLocalizer localizer, IMilvaUserManager<User, int> userManager)
    {
        RuleFor(command => command.Token)
            .NotNullOrEmpty(localizer, MessageKey.AccountTokenInvalid)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.AccountTokenInvalid]);

        RuleFor(command => command.NewPassword)
            .NotNullOrEmpty(localizer, MessageKey.GlobalPassword)
            .IdentityPassword(userManager, localizer);
    }
}
