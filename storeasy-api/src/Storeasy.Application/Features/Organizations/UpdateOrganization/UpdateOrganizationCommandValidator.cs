using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.UpdateOrganization;

/// <summary>
/// Validations of <see cref="UpdateOrganizationCommand"/>.
/// </summary>
public sealed class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    ///<inheritdoc cref="UpdateOrganizationCommandValidator"/>
    public UpdateOrganizationCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .NotNullOrEmpty(localizer, MessageKey.GlobalName)
            .MaximumLength(Organization.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], Organization.NameMaxLength]);
    }
}
