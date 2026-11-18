using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Account.ForgotPassword;

/// <summary>
/// Validations of <see cref="ForgotPasswordCommand"/>.
/// </summary>
public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    ///<inheritdoc cref="ForgotPasswordCommandValidator"/>
    public ForgotPasswordCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Email)
            .NotNullOrEmpty(localizer, MessageKey.GlobalEmail)
            .Email(localizer);
    }
}
