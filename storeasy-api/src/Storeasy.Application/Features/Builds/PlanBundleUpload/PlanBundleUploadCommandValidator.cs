using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Builds.PlanBundleUpload;

/// <summary>
/// Validations of <see cref="PlanBundleUploadCommand"/>.
/// </summary>
public sealed class PlanBundleUploadCommandValidator : AbstractValidator<PlanBundleUploadCommand>
{
    ///<inheritdoc cref="PlanBundleUploadCommandValidator"/>
    public PlanBundleUploadCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.AssetId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsset]]);

        RuleFor(x => x.Status)
            .Must(s => s is "completed" or "draft" or "inProgress")
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrack]]);

        RuleFor(x => x.UserFraction)
            .ExclusiveBetween(0, 1)
            .When(x => x.Status == "inProgress")
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPercentage]]);
    }
}
