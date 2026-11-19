using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.PreReviews.GetPreReviewList;

/// <summary>
/// Validations of <see cref="GetPreReviewListQuery"/>.
/// </summary>
public sealed class GetPreReviewListQueryValidator : AbstractValidator<GetPreReviewListQuery>
{
    ///<inheritdoc cref="GetPreReviewListQueryValidator"/>
    public GetPreReviewListQueryValidator(IMilvaLocalizer localizer)
    {
        this.AppStoreRules(localizer);
    }
}
