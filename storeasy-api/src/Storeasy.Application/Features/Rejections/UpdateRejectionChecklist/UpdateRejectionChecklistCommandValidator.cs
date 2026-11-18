using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionChecklist;

/// <summary>
/// Validations of <see cref="UpdateRejectionChecklistCommand"/>.
/// </summary>
public sealed class UpdateRejectionChecklistCommandValidator : AbstractValidator<UpdateRejectionChecklistCommand>
{
    ///<inheritdoc cref="UpdateRejectionChecklistCommandValidator"/>
    public UpdateRejectionChecklistCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Items)
            .NotNull()
            .Must(items => items.Count <= 100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);

        RuleForEach(command => command.Items)
            .Must(item => !string.IsNullOrWhiteSpace(item.Text) && item.Text.Length <= 500 && Enum.IsDefined(item.Status) && (item.GuidelineCode == null || item.GuidelineCode.Length <= 20))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
