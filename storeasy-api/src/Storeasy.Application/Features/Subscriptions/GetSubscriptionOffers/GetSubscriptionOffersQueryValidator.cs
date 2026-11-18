using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionOffers;

/// <summary>
/// Validations of <see cref="GetSubscriptionOffersQuery"/>.
/// </summary>
public sealed class GetSubscriptionOffersQueryValidator : AbstractValidator<GetSubscriptionOffersQuery>
{
    ///<inheritdoc cref="GetSubscriptionOffersQueryValidator"/>
    public GetSubscriptionOffersQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.SubscriptionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreProduct]]);
    }
}
