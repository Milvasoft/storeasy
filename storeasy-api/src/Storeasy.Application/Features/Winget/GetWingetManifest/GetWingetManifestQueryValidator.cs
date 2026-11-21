using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Winget;

namespace Storeasy.Application.Features.Winget.GetWingetManifest;

/// <summary>
/// Validations of <see cref="GetWingetManifestQuery"/>.
/// </summary>
public sealed class GetWingetManifestQueryValidator : AbstractValidator<GetWingetManifestQuery>
{
    ///<inheritdoc cref="GetWingetManifestQueryValidator"/>
    public GetWingetManifestQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(query => query.Version)
            .Must(version => version.Length <= 128 && WingetFields.PackageVersion().IsMatch(version.Trim()))
            .When(query => !string.IsNullOrWhiteSpace(query.Version))
            .WithMessage(localizer[MessageKey.WingetFieldInvalid, "PackageVersion", string.Empty]);
    }
}
