using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapPricePoints;

/// <summary>
/// Validations of <see cref="GetIapPricePointsQuery"/>.
/// </summary>
public sealed class GetIapPricePointsQueryValidator : AbstractValidator<GetIapPricePointsQuery>
{
    ///<inheritdoc cref="GetIapPricePointsQueryValidator"/>
    public GetIapPricePointsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.IapId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreProduct]]);

        RuleFor(x => x.Territory)
            .NotEmpty()
            .MaximumLength(3)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(GetIapPricePointsQuery.Territory)]);
    }
}
