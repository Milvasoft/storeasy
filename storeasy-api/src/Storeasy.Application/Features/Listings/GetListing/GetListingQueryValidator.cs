using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListing;

/// <summary>
/// Validations of <see cref="GetListingQuery"/>.
/// </summary>
public sealed class GetListingQueryValidator : AbstractValidator<GetListingQuery>
{
    ///<inheritdoc cref="GetListingQueryValidator"/>
    public GetListingQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
