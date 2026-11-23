using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Registries;

namespace Storeasy.Application.Features.Registries.AddTrackedPackage;

/// <summary>
/// Validations of <see cref="AddTrackedPackageCommand"/>.
/// </summary>
public sealed class AddTrackedPackageCommandValidator : AbstractValidator<AddTrackedPackageCommand>
{
    ///<inheritdoc cref="AddTrackedPackageCommandValidator"/>
    public AddTrackedPackageCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Registry)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPackageRegistry]]);

        RuleFor(command => command.Name)
            .Must((command, name) => RegistryNames.IsValid(command.Registry, name))
            .WithMessage(localizer[MessageKey.RegistryPackageNameInvalid]);

        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .When(command => command.AppId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
