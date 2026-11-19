using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Apps.SetAppHidden;

/// <summary>
/// Validations of <see cref="SetAppHiddenCommand"/>.
/// </summary>
public sealed class SetAppHiddenCommandValidator : AbstractValidator<SetAppHiddenCommand>
{
    ///<inheritdoc cref="SetAppHiddenCommandValidator"/>
    public SetAppHiddenCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
