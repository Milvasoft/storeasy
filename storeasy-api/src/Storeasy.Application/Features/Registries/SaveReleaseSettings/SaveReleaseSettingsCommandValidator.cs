using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.SaveReleaseSettings;

/// <summary>
/// Validations of <see cref="SaveReleaseSettingsCommand"/> (the settings themselves are checked by the handler with
/// <c>ReleaseRules.Problems</c>).
/// </summary>
public sealed class SaveReleaseSettingsCommandValidator : AbstractValidator<SaveReleaseSettingsCommand>
{
    /// <summary>
    /// Most version files.
    /// </summary>
    public const int MaxVersionFiles = 10;

    ///<inheritdoc cref="SaveReleaseSettingsCommandValidator"/>
    public SaveReleaseSettingsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command)
            .Must(command => (command.TrackedPackageId > 0) ^ (command.PackageGroupId > 0))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);

        RuleFor(command => command.Settings.VersionFiles)
            .Must(files => files == null || files.Count <= MaxVersionFiles)
            .When(command => command.Settings != null)
            .WithMessage(localizer[MessageKey.RegistryReleaseVersionFilesInvalid]);
    }
}
