using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AccountDtos;
using System.Linq.Expressions;
using System.Text.Json;

namespace Storeasy.Application.Features.Account.GetAccountNotifications;

/// <summary>
/// Handles the query for retrieving the caller's own notifications. The recipient comes from the token, never from the request.
/// </summary>
/// <param name="notificationRepository"></param>
/// <param name="currentUser"></param>
public class GetAccountNotificationsQueryHandler(IStoreasyRepositoryBase<InternalNotification> notificationRepository,
                                                 ICurrentUser currentUser) : IInterceptable, IListQueryHandler<GetAccountNotificationsQuery, AccountNotificationDto>
{
    private readonly IStoreasyRepositoryBase<InternalNotification> _notificationRepository = notificationRepository;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public async Task<ListResponse<AccountNotificationDto>> Handle(GetAccountNotificationsQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);

        var userName = _currentUser.UserName;

        if (string.IsNullOrEmpty(userName))
            return ListResponse<AccountNotificationDto>.Error(default, MessageKey.Unauthorized);

        // IsSeen is computed from SeenDate after the projection, so the database cannot filter on it (the bell's unread
        // counter asks for IsSeen = false): the criterion becomes a condition on SeenDate.
        var seen = TakeIsSeenCriterion(request);

        Expression<Func<InternalNotification, bool>> condition = seen switch
        {
            true => n => n.RecipientUserName == userName && n.SeenDate != null,
            false => n => n.RecipientUserName == userName && n.SeenDate == null,
            null => n => n.RecipientUserName == userName,
        };

        var notifications = await _notificationRepository.GetAllAsync(request,
                                                                      condition: condition,
                                                                      projection: AccountNotificationDto.Projection,
                                                                      cancellationToken: cancellationToken);

        return notifications;
    }

    /// <summary>
    /// Removes the IsSeen criteria from the request and returns the value they ask for (null: none, or not a boolean).
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    private static bool? TakeIsSeenCriterion(GetAccountNotificationsQuery request)
    {
        var criterias = request.Filtering?.Criterias;

        if (criterias == null)
            return null;

        bool? seen = null;

        foreach (var criteria in criterias.Where(c => string.Equals(c.FilterBy, nameof(AccountNotificationDto.IsSeen), StringComparison.OrdinalIgnoreCase)))
            seen = ReadBoolean(criteria.Value) ?? seen;

        request.Filtering.Criterias = [.. criterias.Where(c => !string.Equals(c.FilterBy, nameof(AccountNotificationDto.IsSeen), StringComparison.OrdinalIgnoreCase))];

        return seen;
    }

    private static bool? ReadBoolean(object value) => value switch
    {
        bool boolean => boolean,
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement { ValueKind: JsonValueKind.String } element when bool.TryParse(element.GetString(), out var parsed) => parsed,
        string text when bool.TryParse(text, out var parsed) => parsed,
        _ => null,
    };
}
