using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Assets;

namespace Storeasy.Application.Features.Assets.CompleteAssetUpload;

/// <summary>
/// Validations of <see cref="CompleteAssetUploadCommand"/>.
/// </summary>
public sealed class CompleteAssetUploadCommandValidator : AbstractValidator<CompleteAssetUploadCommand>
{
    ///<inheritdoc cref="CompleteAssetUploadCommandValidator"/>
    public CompleteAssetUploadCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AssetId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsset]]);

        RuleFor(command => command.Parts)
            .Must(parts => parts == null || parts.Count <= AssetRules.MaxPartCount)
            .WithMessage(localizer[MessageKey.AssetNotUploaded]);

        RuleForEach(command => command.Parts)
            .Must(part => part != null && part.PartNumber is > 0 and <= AssetRules.MaxPartCount && !string.IsNullOrWhiteSpace(part.ETag) && part.ETag.Length <= 200)
            .WithMessage(localizer[MessageKey.AssetNotUploaded]);
    }
}
