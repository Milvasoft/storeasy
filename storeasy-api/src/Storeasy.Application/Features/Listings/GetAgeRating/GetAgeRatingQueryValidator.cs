using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetAgeRating;

/// <summary>
/// Validations of <see cref="GetAgeRatingQuery"/>.
/// </summary>
public sealed class GetAgeRatingQueryValidator : AbstractValidator<GetAgeRatingQuery>
{
    ///<inheritdoc cref="GetAgeRatingQueryValidator"/>
    public GetAgeRatingQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
