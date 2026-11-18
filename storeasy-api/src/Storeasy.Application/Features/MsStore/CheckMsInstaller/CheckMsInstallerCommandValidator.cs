using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.CheckMsInstaller;

/// <summary>
/// Validations of <see cref="CheckMsInstallerCommand"/>.
/// </summary>
public sealed class CheckMsInstallerCommandValidator : AbstractValidator<CheckMsInstallerCommand>
{
    ///<inheritdoc cref="CheckMsInstallerCommandValidator"/>
    public CheckMsInstallerCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(command => command.PackageUrls)
            .Must(urls => urls == null || urls.Count <= 20)
            .WithMessage(localizer[MessageKey.MsInstallerInvalid]);

        RuleForEach(command => command.PackageUrls)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && url.Length <= MsInstallerPackage.UrlMaxLength)
            .WithMessage(localizer[MessageKey.MsInstallerInvalid]);
    }
}
