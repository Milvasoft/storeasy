using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanPricing;

/// <summary>
/// Validations of <see cref="PlanPricingCommand"/>.
/// </summary>
public sealed class PlanPricingCommandValidator : AbstractValidator<PlanPricingCommand>
{
    ///<inheritdoc cref="PlanPricingCommandValidator"/>
    public PlanPricingCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
