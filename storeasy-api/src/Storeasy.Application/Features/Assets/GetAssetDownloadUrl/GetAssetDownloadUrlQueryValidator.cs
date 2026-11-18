using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Assets.GetAssetDownloadUrl;

/// <summary>
/// Validations of <see cref="GetAssetDownloadUrlQuery"/>.
/// </summary>
public sealed class GetAssetDownloadUrlQueryValidator : AbstractValidator<GetAssetDownloadUrlQuery>
{
    ///<inheritdoc cref="GetAssetDownloadUrlQueryValidator"/>
    public GetAssetDownloadUrlQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.AssetId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsset]]);
    }
}
