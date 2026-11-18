using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.SaveReviewReplyDraft;

/// <summary>
/// Validations of <see cref="SaveReviewReplyDraftCommand"/>.
/// </summary>
public sealed class SaveReviewReplyDraftCommandValidator : AbstractValidator<SaveReviewReplyDraftCommand>
{
    ///<inheritdoc cref="SaveReviewReplyDraftCommandValidator"/>
    public SaveReviewReplyDraftCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Body)
            .MaximumLength(StoreReview.BodyMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
