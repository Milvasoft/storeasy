using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.PlanRegistryOperation;

/// <summary>
/// Validations of <see cref="PlanRegistryOperationCommand"/>.
/// </summary>
public sealed class PlanRegistryOperationCommandValidator : AbstractValidator<PlanRegistryOperationCommand>
{
    ///<inheritdoc cref="PlanRegistryOperationCommandValidator"/>
    public PlanRegistryOperationCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TrackedPackageId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);

        RuleFor(command => command.Kind)
            .Must(kind => Enum.IsDefined(kind) && kind != RegistryPlanKind.Release)
            .WithMessage(localizer[MessageKey.RegistryOperationUnsupported]);

        RuleFor(command => command.Version)
            .NotEmpty()
            .MaximumLength(TrackedPackage.VersionMaxLength)
            .WithMessage(localizer[MessageKey.RegistryVersionInvalid]);

        RuleFor(command => command.Argument)
            .MaximumLength(TrackedPackage.MessageMaxLength)
            .Must(argument => !TokenReminderRules.LooksLikeToken(argument))
            .WithMessage(localizer[MessageKey.RegistryOperationArgumentInvalid]);

        RuleFor(command => command.Argument)
            .NotEmpty()
            .When(command => command.Kind is RegistryPlanKind.Deprecate or RegistryPlanKind.DistTag)
            .WithMessage(localizer[MessageKey.RegistryOperationArgumentInvalid]);
    }
}
