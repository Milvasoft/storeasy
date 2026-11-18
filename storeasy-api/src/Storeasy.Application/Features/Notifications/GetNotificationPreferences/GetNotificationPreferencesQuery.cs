using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.NotificationDtos;

namespace Storeasy.Application.Features.Notifications.GetNotificationPreferences;

/// <summary>
/// The caller's channels per notification kind (valid in every organization); kinds without a saved choice show their
/// defaults.
/// </summary>
public record GetNotificationPreferencesQuery : IQuery<List<NotificationPreferenceDto>>
{
}
