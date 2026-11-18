using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.GetWebPushSubscriptionList;

/// <summary>
/// Lists the caller's browser push subscriptions, newest first.
/// </summary>
public record GetWebPushSubscriptionListQuery : IQuery<List<WebPushSubscriptionDto>>
{
}
