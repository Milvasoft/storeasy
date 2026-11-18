using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapDetail;

/// <summary>
/// Validations of <see cref="GetIapDetailQuery"/>.
/// </summary>
public sealed class GetIapDetailQueryValidator : AbstractValidator<GetIapDetailQuery>
{
    ///<inheritdoc cref="GetIapDetailQueryValidator"/>
    public GetIapDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.IapId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreProduct]]);
    }
}
