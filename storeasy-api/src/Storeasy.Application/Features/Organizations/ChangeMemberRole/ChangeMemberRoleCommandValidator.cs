using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Organizations.ChangeMemberRole;

/// <summary>
/// Validations of <see cref="ChangeMemberRoleCommand"/>.
/// </summary>
public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    ///<inheritdoc cref="ChangeMemberRoleCommandValidator"/>
    public ChangeMemberRoleCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Role)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.OrganizationRoleInvalid]);
    }
}
