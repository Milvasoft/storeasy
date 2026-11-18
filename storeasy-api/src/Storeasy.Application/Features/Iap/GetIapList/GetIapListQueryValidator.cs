using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapList;

/// <summary>
/// Validations of <see cref="GetIapListQuery"/>.
/// </summary>
public sealed class GetIapListQueryValidator : AbstractValidator<GetIapListQuery>
{
    ///<inheritdoc cref="GetIapListQueryValidator"/>
    public GetIapListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
