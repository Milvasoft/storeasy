using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionPricePoints;

/// <summary>
/// Validations of <see cref="GetSubscriptionPricePointsQuery"/>.
/// </summary>
public sealed class GetSubscriptionPricePointsQueryValidator : AbstractValidator<GetSubscriptionPricePointsQuery>
{
    ///<inheritdoc cref="GetSubscriptionPricePointsQueryValidator"/>
    public GetSubscriptionPricePointsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.SubscriptionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreProduct]]);

        RuleFor(x => x.Territory)
            .NotEmpty()
            .MaximumLength(3)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(GetSubscriptionPricePointsQuery.Territory)]);
    }
}
