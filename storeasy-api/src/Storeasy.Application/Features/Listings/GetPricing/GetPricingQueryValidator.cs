using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetPricing;

/// <summary>
/// Validations of <see cref="GetPricingQuery"/>.
/// </summary>
public sealed class GetPricingQueryValidator : AbstractValidator<GetPricingQuery>
{
    ///<inheritdoc cref="GetPricingQueryValidator"/>
    public GetPricingQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
