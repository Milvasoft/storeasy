using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Helpers;

namespace Storeasy.Application.Features.Reviews.ReplyToReview;

/// <summary>
/// Validations of <see cref="ReplyToReviewCommand"/>. The store's own limit is checked by the handler.
/// </summary>
public sealed class ReplyToReviewCommandValidator : AbstractValidator<ReplyToReviewCommand>
{
    ///<inheritdoc cref="ReplyToReviewCommandValidator"/>
    public ReplyToReviewCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ReviewId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreReview]]);

        RuleFor(command => command.Body)
            .NotEmpty()
            .Must(body => body == null || ReviewRules.Length(body) <= ReviewRules.AppStoreReplyMaxLength)
            .WithMessage(localizer[MessageKey.ReviewReplyTooLong]);
    }
}
