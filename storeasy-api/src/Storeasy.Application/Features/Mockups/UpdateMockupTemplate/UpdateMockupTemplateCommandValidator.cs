using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.UpdateMockupTemplate;

/// <summary>
/// Validations of <see cref="UpdateMockupTemplateCommand"/>.
/// </summary>
public sealed class UpdateMockupTemplateCommandValidator : AbstractValidator<UpdateMockupTemplateCommand>
{
    ///<inheritdoc cref="UpdateMockupTemplateCommandValidator"/>
    public UpdateMockupTemplateCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]]);

        RuleFor(command => command.Version)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(UpdateMockupTemplateCommand.Version)]);

        this.RuleForMockupName(command => command.Name, localizer);
        this.RuleForMockupLocales(command => command.Locales, command => command.PrimaryLocale, localizer);
        this.RuleForMockupSpec(command => command.Spec, localizer, required: true);

        RuleFor(command => command.SlotCount)
            .InclusiveBetween(1, MockupTemplate.MaxSlotCount)
            .WithMessage(localizer[MessageKey.MockupSlotInvalid]);

        RuleForEach(command => command.Slots)
            .Must((command, slot) => slot != null
                                     && slot.Order >= 1 && slot.Order <= command.SlotCount
                                     && Enum.IsDefined(slot.Family)
                                     && slot.AssetId > 0
                                     && (slot.Locale == null || (command.Locales ?? []).Contains(slot.Locale, StringComparer.OrdinalIgnoreCase)))
            .WithMessage(localizer[MessageKey.MockupSlotInvalid]);

        RuleFor(command => command.Slots)
            .Must(slots => slots == null || slots.Where(s => s != null).GroupBy(s => (s.Order, s.Family, Locale: s.Locale?.ToLowerInvariant())).All(g => g.Count() == 1))
            .WithMessage(localizer[MessageKey.MockupSlotInvalid]);
    }
}
