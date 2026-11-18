using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Organizations.UpdateOrganizationRole;

/// <summary>
/// Validations of <see cref="UpdateOrganizationRoleCommand"/>.
/// </summary>
public sealed class UpdateOrganizationRoleCommandValidator : AbstractValidator<UpdateOrganizationRoleCommand>
{
    ///<inheritdoc cref="UpdateOrganizationRoleCommandValidator"/>
    public UpdateOrganizationRoleCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.RoleId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalOrganizationRole]]);

        this.RuleForOrganizationRole(command => command.Name, command => command.Description, command => command.Permissions, localizer);
    }
}
