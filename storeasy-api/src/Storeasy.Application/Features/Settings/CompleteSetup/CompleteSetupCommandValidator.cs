using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Identity.Abstract;
using Storeasy.Application.Behaviours;
using Storeasy.Application.Features.Account.Register;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Settings.CompleteSetup;

/// <summary>
/// Validations of <see cref="CompleteSetupCommand"/>.
/// </summary>
public sealed class CompleteSetupCommandValidator : AbstractValidator<CompleteSetupCommand>
{
    /// <summary>
    /// Longest accepted setup code (a configured code included).
    /// </summary>
    public const int SetupCodeMaxLength = 200;

    ///<inheritdoc cref="CompleteSetupCommandValidator"/>
    public CompleteSetupCommandValidator(IMilvaLocalizer localizer, IMilvaUserManager<User, int> userManager)
    {
        RuleFor(command => command.SetupCode)
            .NotNullOrEmpty(localizer, MessageKey.GlobalSetupCode)
            .MaximumLength(SetupCodeMaxLength)
            .WithMessage(localizer[MessageKey.SetupCodeInvalid]);

        RuleFor(command => command.Email)
            .NotNullOrEmpty(localizer, MessageKey.GlobalEmail)
            .Email(localizer)
            .MaximumLength(RegisterCommandValidator.EmailMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalEmail], RegisterCommandValidator.EmailMaxLength]);

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

        RuleFor(command => command.OrganizationName)
            .NotNullOrEmpty(localizer, MessageKey.GlobalOrganization)
            .MaximumLength(Organization.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalOrganization], Organization.NameMaxLength]);
    }
}
