using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionDetail;

/// <summary>
/// Validations of <see cref="GetSubscriptionDetailQuery"/>.
/// </summary>
public sealed class GetSubscriptionDetailQueryValidator : AbstractValidator<GetSubscriptionDetailQuery>
{
    ///<inheritdoc cref="GetSubscriptionDetailQueryValidator"/>
    public GetSubscriptionDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.SubscriptionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreProduct]]);
    }
}
