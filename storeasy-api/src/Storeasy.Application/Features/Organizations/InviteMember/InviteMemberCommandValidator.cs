using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Organizations.InviteMember;

/// <summary>
/// Validations of <see cref="InviteMemberCommand"/>.
/// </summary>
public sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    ///<inheritdoc cref="InviteMemberCommandValidator"/>
    public InviteMemberCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Email)
            .NotNullOrEmpty(localizer, MessageKey.GlobalEmail)
            .Email(localizer);

        RuleFor(command => command.Role)
            .IsInEnum()
            .NotEqual(OrgRole.Owner)
            .WithMessage(localizer[MessageKey.OrganizationRoleInvalid]);
    }
}
