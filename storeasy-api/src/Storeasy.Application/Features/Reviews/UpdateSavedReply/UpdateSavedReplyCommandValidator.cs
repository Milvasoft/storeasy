using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.UpdateSavedReply;

/// <summary>
/// Validations of <see cref="UpdateSavedReplyCommand"/>.
/// </summary>
public sealed class UpdateSavedReplyCommandValidator : AbstractValidator<UpdateSavedReplyCommand>
{
    ///<inheritdoc cref="UpdateSavedReplyCommandValidator"/>
    public UpdateSavedReplyCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .MaximumLength(SavedReply.NameMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(command => command.Variants)
            .Must(variants => variants == null || variants.Count > 0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);

        RuleForEach(command => command.Variants)
            .Must(SavedReplies.IsValidVariant)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);

        RuleForEach(command => command.Variants)
            .Must(v => v == null || SavedReplies.UnknownVariables(v.Text).Count == 0)
            .WithMessage(localizer[MessageKey.SavedReplyVariableUnknown]);
    }
}
