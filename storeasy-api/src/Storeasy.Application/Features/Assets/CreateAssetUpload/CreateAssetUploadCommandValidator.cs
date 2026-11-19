using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.Assets;

namespace Storeasy.Application.Features.Assets.CreateAssetUpload;

/// <summary>
/// Validations of <see cref="CreateAssetUploadCommand"/>.
/// </summary>
public sealed class CreateAssetUploadCommandValidator : AbstractValidator<CreateAssetUploadCommand>
{
    ///<inheritdoc cref="CreateAssetUploadCommandValidator"/>
    public CreateAssetUploadCommandValidator(IMilvaLocalizer localizer)
    {
        // Organization logos are uploaded through organizations/logo/upload only, which owners may call.
        RuleFor(command => command.Purpose)
            .Must(purpose => Enum.IsDefined(purpose) && purpose != AssetPurpose.OrganizationLogo)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[nameof(AssetPurpose)]]);

        RuleFor(command => command.FileName)
            .NotNullOrEmpty(localizer, MessageKey.GlobalFileName)
            .MaximumLength(Asset.FileNameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalFileName], Asset.FileNameMaxLength]);

        RuleFor(command => command.ContentType)
            .NotNullOrEmpty(localizer, MessageKey.GlobalContentType)
            .MaximumLength(Asset.ContentTypeMaxLength)
            .Matches("^[A-Za-z0-9][A-Za-z0-9.+-]*/[A-Za-z0-9][A-Za-z0-9.+-]*$")
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalContentType]]);

        RuleFor(command => command.Size)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalFileSize]]);

        RuleFor(command => command.Size)
            .Must((command, size) => size <= AssetRules.MaxBytes(command.Purpose))
            .WithMessage(command => localizer[MessageKey.AssetTooLarge, AssetRules.MaxBytes(command.Purpose) / (1024 * 1024)]);

        RuleFor(command => command.Sha256)
            .Matches("^[0-9a-fA-F]{64}$")
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, "SHA-256"])
            .When(command => !string.IsNullOrEmpty(command.Sha256));
    }
}
