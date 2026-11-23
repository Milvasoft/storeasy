using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.PlanRegistryRelease;

/// <summary>
/// Validations of <see cref="PlanRegistryReleaseCommand"/>.
/// </summary>
public sealed class PlanRegistryReleaseCommandValidator : AbstractValidator<PlanRegistryReleaseCommand>
{
    ///<inheritdoc cref="PlanRegistryReleaseCommandValidator"/>
    public PlanRegistryReleaseCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command)
            .Must(command => (command.TrackedPackageId > 0) ^ (command.PackageGroupId > 0))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);

        RuleFor(command => command.Version)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage(localizer[MessageKey.RegistryVersionInvalid]);
    }
}
