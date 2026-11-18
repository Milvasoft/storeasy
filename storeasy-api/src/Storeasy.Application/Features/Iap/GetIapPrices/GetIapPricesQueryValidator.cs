using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapPrices;

/// <summary>
/// Validations of <see cref="GetIapPricesQuery"/>.
/// </summary>
public sealed class GetIapPricesQueryValidator : AbstractValidator<GetIapPricesQuery>
{
    ///<inheritdoc cref="GetIapPricesQueryValidator"/>
    public GetIapPricesQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.IapId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreProduct]]);
    }
}
