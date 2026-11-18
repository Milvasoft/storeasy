using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Identity.Abstract;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Account.Register;

/// <summary>
/// Validations of <see cref="RegisterCommand"/>.
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    /// <summary>
    /// Longest accepted e-mail address (the user name column limit).
    /// </summary>
    public const int EmailMaxLength = 256;

    ///<inheritdoc cref="RegisterCommandValidator"/>
    public RegisterCommandValidator(IMilvaLocalizer localizer, IMilvaUserManager<User, int> userManager)
    {
        RuleFor(command => command.Email)
            .NotNullOrEmpty(localizer, MessageKey.GlobalEmail)
            .Email(localizer)
            .MaximumLength(EmailMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalEmail], EmailMaxLength]);

        RuleFor(command => command.Password)
            .NotNullOrEmpty(localizer, MessageKey.GlobalPassword)
            .IdentityPassword(userManager, localizer);

        RuleFor(command => command.Name)
            .NotNullOrEmpty(localizer, MessageKey.GlobalName)
            .MaximumLength(User.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], User.NameMaxLength]);

        RuleFor(command => command.Surname)
            .MaximumLength(User.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalSurname], User.NameMaxLength]);

        RuleFor(command => command.InvitationToken)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.InvitationInvalid]);
    }
}
