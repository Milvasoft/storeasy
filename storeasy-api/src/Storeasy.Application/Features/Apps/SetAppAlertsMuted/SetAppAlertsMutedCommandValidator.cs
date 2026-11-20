using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Apps.SetAppAlertsMuted;

/// <summary>
/// Validations of <see cref="SetAppAlertsMutedCommand"/>.
/// </summary>
public sealed class SetAppAlertsMutedCommandValidator : AbstractValidator<SetAppAlertsMutedCommand>
{
    ///<inheritdoc cref="SetAppAlertsMutedCommandValidator"/>
    public SetAppAlertsMutedCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
