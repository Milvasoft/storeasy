using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListingDetails;

/// <summary>
/// Validations of <see cref="GetListingDetailsQuery"/>.
/// </summary>
public sealed class GetListingDetailsQueryValidator : AbstractValidator<GetListingDetailsQuery>
{
    ///<inheritdoc cref="GetListingDetailsQueryValidator"/>
    public GetListingDetailsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
