using FluentValidation;

namespace Storeasy.Application.Features.Account.ResendEmailVerification;

/// <summary>
/// Validations of <see cref="ResendEmailVerificationCommand"/>.
/// </summary>
public sealed class ResendEmailVerificationCommandValidator : AbstractValidator<ResendEmailVerificationCommand>
{
    ///<inheritdoc cref="ResendEmailVerificationCommandValidator"/>
    public ResendEmailVerificationCommandValidator()
    {
    }
}
