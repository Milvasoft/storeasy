using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.GetAccountNotifications;

/// <summary>
/// Query of the caller's own notifications (the recipient is taken from the token).
/// </summary>
public record GetAccountNotificationsQuery : ListRequest, IListRequestQuery<AccountNotificationDto>
{
}
