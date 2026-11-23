using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.SaveTokenReminder;

/// <summary>
/// Validations of <see cref="SaveTokenReminderCommand"/>. A label or a "used in" text that looks like a token value is
/// refused with <see cref="MessageKey.RegistryTokenValueRefused"/>: storeasy never takes a registry token.
/// </summary>
public sealed class SaveTokenReminderCommandValidator : AbstractValidator<SaveTokenReminderCommand>
{
    ///<inheritdoc cref="SaveTokenReminderCommandValidator"/>
    public SaveTokenReminderCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Registry)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPackageRegistry]]);

        RuleFor(command => command.Label)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]])
            .MaximumLength(RegistryTokenReminder.LabelMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], RegistryTokenReminder.LabelMaxLength])
            .Must(label => !TokenReminderRules.LooksLikeToken(label))
            .WithMessage(localizer[MessageKey.RegistryTokenValueRefused]);

        RuleFor(command => command.UsedIn)
            .MaximumLength(RegistryTokenReminder.UsedInMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalTokenReminder], RegistryTokenReminder.UsedInMaxLength])
            .Must(usedIn => !TokenReminderRules.LooksLikeToken(usedIn))
            .WithMessage(localizer[MessageKey.RegistryTokenValueRefused]);

        RuleFor(command => command.ExpiryDate)
            .Must(date => date.Year >= 2000 && date.Year <= 2100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTokenReminder]]);

        RuleFor(command => command.Id)
            .GreaterThan(0)
            .When(command => command.Id.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTokenReminder]]);

        RuleFor(command => command.TrackedPackageId)
            .GreaterThan(0)
            .When(command => command.TrackedPackageId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);
    }
}
