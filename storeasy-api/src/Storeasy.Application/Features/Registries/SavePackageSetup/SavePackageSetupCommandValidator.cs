using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Registries;

namespace Storeasy.Application.Features.Registries.SavePackageSetup;

/// <summary>
/// Validations of <see cref="SavePackageSetupCommand"/>.
/// </summary>
public sealed class SavePackageSetupCommandValidator : AbstractValidator<SavePackageSetupCommand>
{
    ///<inheritdoc cref="SavePackageSetupCommandValidator"/>
    public SavePackageSetupCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TrackedPackageId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);

        When(command => command.Provider.HasValue, () =>
        {
            RuleFor(command => command.Provider)
                .IsInEnum()
                .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalCiProvider]]);

            RuleFor(command => command.Repository)
                .Must((command, repository) => TrustedPublishing.IsRepository(command.Provider.Value, repository?.Trim()))
                .WithMessage(localizer[MessageKey.RegistrySetupRepositoryInvalid]);

            RuleFor(command => command.Workflow)
                .Must((command, workflow) => TrustedPublishing.IsWorkflow(command.Provider.Value, workflow?.Trim()))
                .WithMessage(localizer[MessageKey.RegistrySetupWorkflowInvalid]);

            RuleFor(command => command.Environment)
                .Must(environment => TrustedPublishing.IsName(environment.Trim()))
                .When(command => !string.IsNullOrWhiteSpace(command.Environment))
                .WithMessage(localizer[MessageKey.RegistrySetupNameInvalid]);

            RuleFor(command => command.RegistryUser)
                .Must(user => TrustedPublishing.IsName(user.Trim()) && !TokenReminderRules.LooksLikeToken(user))
                .When(command => !string.IsNullOrWhiteSpace(command.RegistryUser))
                .WithMessage(localizer[MessageKey.RegistrySetupNameInvalid]);
        });
    }
}
