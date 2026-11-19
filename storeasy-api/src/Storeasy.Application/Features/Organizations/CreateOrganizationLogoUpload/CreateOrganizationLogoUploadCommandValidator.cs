using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.CreateOrganizationLogoUpload;

/// <summary>
/// Validations of <see cref="CreateOrganizationLogoUploadCommand"/>.
/// </summary>
public sealed class CreateOrganizationLogoUploadCommandValidator : AbstractValidator<CreateOrganizationLogoUploadCommand>
{
    ///<inheritdoc cref="CreateOrganizationLogoUploadCommandValidator"/>
    public CreateOrganizationLogoUploadCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ContentType)
            .Must(OrganizationLogoRules.IsAcceptedContentType)
            .WithMessage(localizer[MessageKey.OrganizationLogoInvalid]);

        RuleFor(command => command.Size)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalFileSize]]);

        RuleFor(command => command.Size)
            .LessThanOrEqualTo(OrganizationLogoRules.MaxBytes)
            .WithMessage(localizer[MessageKey.AssetTooLarge, OrganizationLogoRules.MaxBytes / (1024 * 1024)]);
    }
}
