using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.RenderMockupPreview;

/// <summary>
/// Validations of <see cref="RenderMockupPreviewQuery"/>.
/// </summary>
public sealed class RenderMockupPreviewQueryValidator : AbstractValidator<RenderMockupPreviewQuery>
{
    ///<inheritdoc cref="RenderMockupPreviewQueryValidator"/>
    public RenderMockupPreviewQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.TemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]]);

        RuleFor(query => query.Locale)
            .Must(MockupSpecRules.IsLocale)
            .WithMessage(query => localizer[MessageKey.MockupLocaleInvalid, query.Locale])
            .When(query => query.Locale != null);

        RuleFor(query => query.ClassKey)
            .Must(StudioClassMap.IsKnown)
            .WithMessage(query => localizer[MessageKey.MockupClassUnknown, query.ClassKey])
            .When(query => query.ClassKey != null);

        RuleFor(query => query.SlotOrders)
            .Must(slots => slots.Count is > 0 and <= RenderMockupPreviewQuery.MaxSlots && slots.All(s => s is >= 1 and <= MockupTemplate.MaxSlotCount))
            .WithMessage(localizer[MessageKey.MockupSlotInvalid])
            .When(query => query.SlotOrders != null);

        RuleFor(query => query.MaxWidth)
            .GreaterThanOrEqualTo(RenderMockupPreviewQuery.MinWidth)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(RenderMockupPreviewQuery.MaxWidth)])
            .When(query => query.MaxWidth != null);

        this.RuleForMockupSpec(query => query.Spec, localizer, required: false);
    }
}
