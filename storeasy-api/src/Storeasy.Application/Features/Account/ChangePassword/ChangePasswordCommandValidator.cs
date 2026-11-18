using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Identity.Abstract;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Account.ChangePassword;

/// <summary>
/// Validations of <see cref="ChangePasswordCommand"/>.
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    ///<inheritdoc cref="ChangePasswordCommandValidator"/>
    public ChangePasswordCommandValidator(IMilvaLocalizer localizer, IMilvaUserManager<User, int> userManager)
    {
        RuleFor(query => query.UserName)
            .NotEmpty()
            .NotNull()
            .WithMessage(localizer[MessageKey.IdentityInvalidUserName]);

        RuleFor(query => query.OldPassword)
            .NotEmpty()
            .NotNull()
            .WithMessage(localizer[MessageKey.CannotBeEmpty, localizer[nameof(ChangePasswordCommand.OldPassword)]]);

        RuleFor(query => query.NewPassword)
            .NotEmpty()
            .NotNull()
            .WithMessage(localizer[MessageKey.CannotBeEmpty, localizer[nameof(ChangePasswordCommand.NewPassword)]])
            .IdentityPassword(userManager, localizer);
    }
}