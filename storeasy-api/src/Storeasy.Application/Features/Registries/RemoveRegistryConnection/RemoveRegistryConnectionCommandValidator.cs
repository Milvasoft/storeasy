using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.RemoveRegistryConnection;

/// <summary>
/// Validations of <see cref="RemoveRegistryConnectionCommand"/>.
/// </summary>
public sealed class RemoveRegistryConnectionCommandValidator : AbstractValidator<RemoveRegistryConnectionCommand>
{
    ///<inheritdoc cref="RemoveRegistryConnectionCommandValidator"/>
    public RemoveRegistryConnectionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRegistryConnection]]);
    }
}
