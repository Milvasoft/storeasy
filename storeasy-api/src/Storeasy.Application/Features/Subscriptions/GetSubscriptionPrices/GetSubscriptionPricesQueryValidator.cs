using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionPrices;

/// <summary>
/// Validations of <see cref="GetSubscriptionPricesQuery"/>.
/// </summary>
public sealed class GetSubscriptionPricesQueryValidator : AbstractValidator<GetSubscriptionPricesQuery>
{
    ///<inheritdoc cref="GetSubscriptionPricesQueryValidator"/>
    public GetSubscriptionPricesQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.SubscriptionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreProduct]]);
    }
}
