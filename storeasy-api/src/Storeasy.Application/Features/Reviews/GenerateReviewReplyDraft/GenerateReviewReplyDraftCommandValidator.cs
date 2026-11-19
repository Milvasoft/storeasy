using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Reviews.GenerateReviewReplyDraft;

/// <summary>
/// Validations of <see cref="GenerateReviewReplyDraftCommand"/>.
/// </summary>
public sealed class GenerateReviewReplyDraftCommandValidator : AbstractValidator<GenerateReviewReplyDraftCommand>
{
    ///<inheritdoc cref="GenerateReviewReplyDraftCommandValidator"/>
    public GenerateReviewReplyDraftCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ReviewId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreReview]]);
    }
}
