using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.ConvertSubscriptionPrices;

/// <summary>
/// Validations of <see cref="ConvertSubscriptionPricesQuery"/>.
/// </summary>
public sealed class ConvertSubscriptionPricesQueryValidator : AbstractValidator<ConvertSubscriptionPricesQuery>
{
    ///<inheritdoc cref="ConvertSubscriptionPricesQueryValidator"/>
    public ConvertSubscriptionPricesQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(ConvertSubscriptionPricesQuery.Currency)]);

        RuleFor(x => x.Amount)
            .Must((query, amount) => MonetizationRules.IsValidAmount(query.Currency, amount))
            .WithMessage(localizer[MessageKey.MonetizationPriceInvalid]);
    }
}
