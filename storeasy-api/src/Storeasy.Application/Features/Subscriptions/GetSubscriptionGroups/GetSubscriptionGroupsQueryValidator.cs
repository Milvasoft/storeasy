using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionGroups;

/// <summary>
/// Validations of <see cref="GetSubscriptionGroupsQuery"/>.
/// </summary>
public sealed class GetSubscriptionGroupsQueryValidator : AbstractValidator<GetSubscriptionGroupsQuery>
{
    ///<inheritdoc cref="GetSubscriptionGroupsQueryValidator"/>
    public GetSubscriptionGroupsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
