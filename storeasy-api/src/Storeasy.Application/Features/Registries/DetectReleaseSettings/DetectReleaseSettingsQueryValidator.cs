using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.DetectReleaseSettings;

/// <summary>
/// Validations of <see cref="DetectReleaseSettingsQuery"/>.
/// </summary>
public sealed class DetectReleaseSettingsQueryValidator : AbstractValidator<DetectReleaseSettingsQuery>
{
    ///<inheritdoc cref="DetectReleaseSettingsQueryValidator"/>
    public DetectReleaseSettingsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query)
            .Must(query => (query.TrackedPackageId > 0) ^ (query.PackageGroupId > 0))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);

        RuleFor(query => query.Repository)
            .Matches("^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$")
            .When(query => !string.IsNullOrEmpty(query.Repository))
            .WithMessage(localizer[MessageKey.RegistrySetupRepositoryInvalid]);
    }
}
