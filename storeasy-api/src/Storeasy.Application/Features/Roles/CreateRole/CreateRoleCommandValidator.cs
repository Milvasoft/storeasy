using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Roles.CreateRole;

/// <summary>
/// Validations of <see cref="CreateRoleCommand"/>.
/// </summary>
public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    ///<inheritdoc cref="CreateRoleCommandValidator"/>
    public CreateRoleCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.Name)
            .NotNullOrEmpty(localizer, MessageKey.GlobalName);
    }
}