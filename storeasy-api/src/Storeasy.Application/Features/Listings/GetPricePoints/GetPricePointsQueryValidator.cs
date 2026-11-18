using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetPricePoints;

/// <summary>
/// Validations of <see cref="GetPricePointsQuery"/>.
/// </summary>
public sealed class GetPricePointsQueryValidator : AbstractValidator<GetPricePointsQuery>
{
    ///<inheritdoc cref="GetPricePointsQueryValidator"/>
    public GetPricePointsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
