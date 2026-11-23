using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.GetReleaseSettings;

/// <summary>
/// Validations of <see cref="GetReleaseSettingsQuery"/>.
/// </summary>
public sealed class GetReleaseSettingsQueryValidator : AbstractValidator<GetReleaseSettingsQuery>
{
    ///<inheritdoc cref="GetReleaseSettingsQueryValidator"/>
    public GetReleaseSettingsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query)
            .Must(query => (query.TrackedPackageId > 0) ^ (query.PackageGroupId > 0))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);

        RuleFor(query => query.Prerelease)
            .Matches("^[0-9A-Za-z.-]{1,20}$")
            .When(query => !string.IsNullOrEmpty(query.Prerelease))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRegistryPlan]]);
    }
}
