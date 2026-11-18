using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Account.VerifyEmail;

/// <summary>
/// Validations of <see cref="VerifyEmailCommand"/>.
/// </summary>
public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    ///<inheritdoc cref="VerifyEmailCommandValidator"/>
    public VerifyEmailCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Token)
            .NotNullOrEmpty(localizer, MessageKey.AccountTokenInvalid)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.AccountTokenInvalid]);
    }
}
