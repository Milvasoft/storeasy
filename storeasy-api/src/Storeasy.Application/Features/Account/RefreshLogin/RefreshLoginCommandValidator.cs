using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Features.Account.Logout;

namespace Storeasy.Application.Features.Account.RefreshLogin;

/// <summary>
/// Validations of <see cref="RefreshLoginCommand"/>.
/// </summary>
public sealed class RefreshLoginCommandValidator : AbstractValidator<RefreshLoginCommand>
{
    ///<inheritdoc cref="LogoutCommandValidator"/>
    public RefreshLoginCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.UserName)
            .NotEmpty()
            .NotNull()
            .WithMessage(localizer[MessageKey.IdentityInvalidUserName]);

        RuleFor(query => query.DeviceId)
            .NotEmpty()
            .NotNull()
            .WithMessage(localizer[MessageKey.PossibleUIError]);

        RuleFor(query => query.RefreshToken)
            .NotEmpty()
            .NotNull()
            .WithMessage(localizer[MessageKey.PossibleUIError]);
    }
}