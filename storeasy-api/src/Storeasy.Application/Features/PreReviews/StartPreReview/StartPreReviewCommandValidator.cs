using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.PreReviews.StartPreReview;

/// <summary>
/// Validations of <see cref="StartPreReviewCommand"/>.
/// </summary>
public sealed class StartPreReviewCommandValidator : AbstractValidator<StartPreReviewCommand>
{
    ///<inheritdoc cref="StartPreReviewCommandValidator"/>
    public StartPreReviewCommandValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);
    }
}
