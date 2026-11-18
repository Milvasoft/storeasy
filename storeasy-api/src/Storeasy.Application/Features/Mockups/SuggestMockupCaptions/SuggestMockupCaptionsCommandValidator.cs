using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.SuggestMockupCaptions;

/// <summary>
/// Validations of <see cref="SuggestMockupCaptionsCommand"/>.
/// </summary>
public sealed class SuggestMockupCaptionsCommandValidator : AbstractValidator<SuggestMockupCaptionsCommand>
{
    ///<inheritdoc cref="SuggestMockupCaptionsCommandValidator"/>
    public SuggestMockupCaptionsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]]);

        RuleFor(command => command.TargetLocale)
            .Must(MockupSpecRules.IsLocale)
            .WithMessage(command => localizer[MessageKey.MockupLocaleInvalid, command.TargetLocale ?? string.Empty]);

        RuleForEach(command => command.SourceLocales)
            .Must(MockupSpecRules.IsLocale)
            .WithMessage((_, locale) => localizer[MessageKey.MockupLocaleInvalid, locale ?? string.Empty]);

        RuleForEach(command => command.SlotOrders)
            .InclusiveBetween(1, MockupTemplate.MaxSlotCount)
            .WithMessage(localizer[MessageKey.MockupSlotInvalid]);
    }
}
