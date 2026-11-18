using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListingByField;

/// <summary>
/// Validations of <see cref="GetListingByFieldQuery"/>.
/// </summary>
public sealed class GetListingByFieldQueryValidator : AbstractValidator<GetListingByFieldQuery>
{
    ///<inheritdoc cref="GetListingByFieldQueryValidator"/>
    public GetListingByFieldQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Field)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalField]]);
    }
}
