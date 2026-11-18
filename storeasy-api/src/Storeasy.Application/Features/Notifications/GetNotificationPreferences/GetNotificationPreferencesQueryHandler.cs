using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Notifications.GetNotificationPreferences;

/// <summary>
/// Handles <see cref="GetNotificationPreferencesQuery"/>.
/// </summary>
/// <param name="PreferenceRepository"></param>
/// <param name="CurrentUser"></param>
public record GetNotificationPreferencesQueryHandler(IStoreasyRepositoryBase<NotificationPreference> PreferenceRepository, ICurrentUser CurrentUser) : IInterceptable, IQueryHandler<GetNotificationPreferencesQuery, List<NotificationPreferenceDto>>
{
    private readonly IStoreasyRepositoryBase<NotificationPreference> _preferenceRepository = PreferenceRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<List<NotificationPreferenceDto>>> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();

        var saved = (await _preferenceRepository.GetAllAsync(p => p.UserId == userId,
                                                             projection: p => new NotificationPreference { Kind = p.Kind, InApp = p.InApp, Email = p.Email, WebPush = p.WebPush },
                                                             cancellationToken: cancellationToken)).ToDictionary(p => p.Kind);

        var preferences = NotificationKinds.Configurable.Select(kind =>
        {
            var (inApp, email) = saved.TryGetValue(kind, out var row) ? (row.InApp, row.Email) : NotificationKinds.Default(kind);
            var effective = NotificationKinds.Effective(kind, inApp, email);

            return new NotificationPreferenceDto
            {
                Kind = kind,
                InApp = effective.InApp,
                Email = effective.Email,
                WebPush = row?.WebPush ?? false,
                Locked = NotificationKinds.IsLocked(kind),
            };
        }).ToList();

        return Response<List<NotificationPreferenceDto>>.Success(preferences);
    }
}
