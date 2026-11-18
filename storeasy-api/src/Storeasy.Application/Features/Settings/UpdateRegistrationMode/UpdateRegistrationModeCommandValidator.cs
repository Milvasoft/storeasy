using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Settings.UpdateRegistrationMode;

/// <summary>
/// Validations of <see cref="UpdateRegistrationModeCommand"/>.
/// </summary>
public sealed class UpdateRegistrationModeCommandValidator : AbstractValidator<UpdateRegistrationModeCommand>
{
    ///<inheritdoc cref="UpdateRegistrationModeCommandValidator"/>
    public UpdateRegistrationModeCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Mode)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRegistrationMode]]);
    }
}
