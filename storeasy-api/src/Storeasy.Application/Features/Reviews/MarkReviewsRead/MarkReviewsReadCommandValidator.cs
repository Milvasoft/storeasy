using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Reviews.MarkReviewsRead;

/// <summary>
/// Validations of <see cref="MarkReviewsReadCommand"/>.
/// </summary>
public sealed class MarkReviewsReadCommandValidator : AbstractValidator<MarkReviewsReadCommand>
{
    ///<inheritdoc cref="MarkReviewsReadCommandValidator"/>
    public MarkReviewsReadCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ReviewIds)
            .NotEmpty()
            .Must(ids => ids.Count <= 500)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreReview]]);
    }
}
