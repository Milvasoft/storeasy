using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Listings.GetReviewInfo;

/// <summary>
/// Validations of <see cref="GetReviewInfoQuery"/>.
/// </summary>
public sealed class GetReviewInfoQueryValidator : AbstractValidator<GetReviewInfoQuery>
{
    ///<inheritdoc cref="GetReviewInfoQueryValidator"/>
    public GetReviewInfoQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
