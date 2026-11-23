using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.RemoveTrackedPackage;

/// <summary>
/// Validations of <see cref="RemoveTrackedPackageCommand"/>.
/// </summary>
public sealed class RemoveTrackedPackageCommandValidator : AbstractValidator<RemoveTrackedPackageCommand>
{
    ///<inheritdoc cref="RemoveTrackedPackageCommandValidator"/>
    public RemoveTrackedPackageCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TrackedPackageId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);
    }
}
