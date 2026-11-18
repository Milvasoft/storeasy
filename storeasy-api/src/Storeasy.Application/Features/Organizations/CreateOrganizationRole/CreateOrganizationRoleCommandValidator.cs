using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Organizations.CreateOrganizationRole;

/// <summary>
/// Validations of <see cref="CreateOrganizationRoleCommand"/>.
/// </summary>
public sealed class CreateOrganizationRoleCommandValidator : AbstractValidator<CreateOrganizationRoleCommand>
{
    ///<inheritdoc cref="CreateOrganizationRoleCommandValidator"/>
    public CreateOrganizationRoleCommandValidator(IMilvaLocalizer localizer)
    {
        this.RuleForOrganizationRole(command => command.Name, command => command.Description, command => command.Permissions, localizer);
    }
}
