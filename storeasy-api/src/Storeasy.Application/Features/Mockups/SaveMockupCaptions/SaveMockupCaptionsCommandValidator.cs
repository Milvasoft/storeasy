using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.SaveMockupCaptions;

/// <summary>
/// Validations of <see cref="SaveMockupCaptionsCommand"/>.
/// </summary>
public sealed class SaveMockupCaptionsCommandValidator : AbstractValidator<SaveMockupCaptionsCommand>
{
    ///<inheritdoc cref="SaveMockupCaptionsCommandValidator"/>
    public SaveMockupCaptionsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]]);

        RuleFor(command => command.Items)
            .Must(items => items != null && items.Count is > 0 and <= SaveMockupCaptionsCommand.MaxItems && items.All(i => i != null))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalCaption]]);

        RuleFor(command => command.Items)
            .Must(items => items == null || items.Where(i => i != null).GroupBy(i => (Locale: i.Locale?.ToLowerInvariant(), i.SlotOrder)).All(g => g.Count() == 1))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalCaption]]);

        RuleForEach(command => command.Items)
            .Must(item => item == null || MockupSpecRules.IsLocale(item.Locale))
            .WithMessage((_, item) => localizer[MessageKey.MockupLocaleInvalid, item?.Locale ?? string.Empty])
            .Must(item => item == null || item.SlotOrder is >= 1 and <= MockupTemplate.MaxSlotCount)
            .WithMessage(localizer[MessageKey.MockupSlotInvalid])
            .Must(item => item == null || (item.Title?.Length ?? 0) <= MockupCaption.TitleMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalCaption], MockupCaption.TitleMaxLength])
            .Must(item => item == null || (item.Subtitle?.Length ?? 0) <= MockupCaption.SubtitleMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalCaption], MockupCaption.SubtitleMaxLength]);
    }
}
