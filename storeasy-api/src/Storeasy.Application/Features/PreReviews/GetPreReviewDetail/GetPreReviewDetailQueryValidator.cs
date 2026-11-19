using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.PreReviews.GetPreReviewDetail;

/// <summary>
/// Validations of <see cref="GetPreReviewDetailQuery"/>.
/// </summary>
public sealed class GetPreReviewDetailQueryValidator : AbstractValidator<GetPreReviewDetailQuery>
{
    ///<inheritdoc cref="GetPreReviewDetailQueryValidator"/>
    public GetPreReviewDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPreReview]]);
    }
}
