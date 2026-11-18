using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Organizations.AcceptInvitation;

/// <summary>
/// Validations of <see cref="AcceptInvitationCommand"/>.
/// </summary>
public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    ///<inheritdoc cref="AcceptInvitationCommandValidator"/>
    public AcceptInvitationCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Token)
            .NotNullOrEmpty(localizer, MessageKey.InvitationInvalid)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.InvitationInvalid]);
    }
}
