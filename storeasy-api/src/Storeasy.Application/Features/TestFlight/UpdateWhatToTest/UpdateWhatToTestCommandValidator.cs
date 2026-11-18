using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.UpdateWhatToTest;

/// <summary>
/// Validations of <see cref="UpdateWhatToTestCommand"/>.
/// </summary>
public sealed class UpdateWhatToTestCommandValidator : AbstractValidator<UpdateWhatToTestCommand>
{
    ///<inheritdoc cref="UpdateWhatToTestCommandValidator"/>
    public UpdateWhatToTestCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Locale)
            .NotEmpty()
            .MaximumLength(20)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalLocale]]);

        RuleFor(command => command.WhatsNew)
            .NotNull()
            .MaximumLength(BetaBuild.WhatToTestMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
