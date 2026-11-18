using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Notifications.UpdateNotificationPreference;

/// <summary>
/// Handles <see cref="UpdateNotificationPreferenceCommand"/>.
/// </summary>
/// <param name="PreferenceRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
public record UpdateNotificationPreferenceCommandHandler(IStoreasyRepositoryBase<NotificationPreference> PreferenceRepository,
                                                         ICurrentUser CurrentUser,
                                                         TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UpdateNotificationPreferenceCommand, NotificationPreferenceDto>
{
    private readonly IStoreasyRepositoryBase<NotificationPreference> _preferenceRepository = PreferenceRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<NotificationPreferenceDto>> Handle(UpdateNotificationPreferenceCommand request, CancellationToken cancellationToken)
    {
        if (NotificationKinds.IsLocked(request.Kind) && (!request.InApp || !request.Email))
            return Response<NotificationPreferenceDto>.Error(null, MessageKey.NotificationKindLocked);

        var userId = _currentUser.GetRequiredId();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var updated = await _preferenceRepository.ExecuteUpdateAsync(p => p.UserId == userId && p.Kind == request.Kind,
                                                                     new SetPropertyBuilder<NotificationPreference>().SetPropertyValue(p => p.InApp, request.InApp)
                                                                                                                     .SetPropertyValue(p => p.Email, request.Email)
                                                                                                                     .SetPropertyValue(p => p.WebPush, request.WebPush)
                                                                                                                     .SetPropertyValue(p => p.UpdatedDate, now),
                                                                     cancellationToken);

        if (updated == 0)
        {
            await _preferenceRepository.AddAsync(new NotificationPreference
            {
                UserId = userId,
                Kind = request.Kind,
                InApp = request.InApp,
                Email = request.Email,
                WebPush = request.WebPush,
                UpdatedDate = now,
            }, cancellationToken);
        }

        return Response<NotificationPreferenceDto>.Success(new NotificationPreferenceDto
        {
            Kind = request.Kind,
            InApp = request.InApp,
            Email = request.Email,
            WebPush = request.WebPush,
            Locked = NotificationKinds.IsLocked(request.Kind),
        });
    }
}
