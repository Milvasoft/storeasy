using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.SyncTrackedPackage;

/// <summary>
/// Validations of <see cref="SyncTrackedPackageCommand"/>.
/// </summary>
public sealed class SyncTrackedPackageCommandValidator : AbstractValidator<SyncTrackedPackageCommand>
{
    ///<inheritdoc cref="SyncTrackedPackageCommandValidator"/>
    public SyncTrackedPackageCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TrackedPackageId)
            .GreaterThan(0)
            .When(command => command.TrackedPackageId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);
    }
}
