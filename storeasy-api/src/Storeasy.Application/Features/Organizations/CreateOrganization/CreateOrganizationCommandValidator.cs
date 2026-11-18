using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.CreateOrganization;

/// <summary>
/// Validations of <see cref="CreateOrganizationCommand"/>.
/// </summary>
public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    ///<inheritdoc cref="CreateOrganizationCommandValidator"/>
    public CreateOrganizationCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .NotNullOrEmpty(localizer, MessageKey.GlobalName)
            .MaximumLength(Organization.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], Organization.NameMaxLength]);

        RuleFor(command => command.Slug)
            .Must((command, slug) => OrgRoleRules.ToSlug(string.IsNullOrWhiteSpace(slug) ? command.Name : slug).Length >= 3)
            .WithMessage(localizer[MessageKey.OrganizationSlugInvalid])
            .When(command => !string.IsNullOrWhiteSpace(command.Name));
    }
}
