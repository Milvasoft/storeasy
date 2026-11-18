using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Application.Features.Account.SaveWebPushSubscription;

/// <summary>
/// Handles <see cref="SaveWebPushSubscriptionCommand"/>. A user keeps at most <see cref="WebPushSubscription.MaxPerUser"/>
/// browsers; the oldest beyond that are dropped.
/// </summary>
/// <param name="SubscriptionRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="Options"></param>
public record SaveWebPushSubscriptionCommandHandler(IStoreasyRepositoryBase<WebPushSubscription> SubscriptionRepository,
                                                    ICurrentUser CurrentUser,
                                                    WebPushOptions Options) : IInterceptable, ICommandHandler<SaveWebPushSubscriptionCommand, int>
{
    private readonly IStoreasyRepositoryBase<WebPushSubscription> _subscriptionRepository = SubscriptionRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly WebPushOptions _options = Options;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SaveWebPushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Response<int>.Error(0, MessageKey.WebPushDisabled);

        var userId = _currentUser.GetRequiredId();
        var endpoint = request.Endpoint.Trim();
        var userAgent = request.UserAgent?.Length > 300 ? request.UserAgent[..300] : request.UserAgent;

        var existing = await _subscriptionRepository.GetFirstOrDefaultAsync(s => s.Endpoint == endpoint, projection: s => (int?)s.Id, cancellationToken: cancellationToken);

        if (existing.HasValue)
        {
            await _subscriptionRepository.ExecuteUpdateAsync(existing.Value,
                                                             new SetPropertyBuilder<WebPushSubscription>().SetPropertyValue(s => s.UserId, userId)
                                                                                                          .SetPropertyValue(s => s.P256dh, request.P256dh.Trim())
                                                                                                          .SetPropertyValue(s => s.Auth, request.Auth.Trim())
                                                                                                          .SetPropertyValue(s => s.UserAgent, userAgent),
                                                             cancellationToken);

            return Response<int>.Success(existing.Value);
        }

        var subscription = new WebPushSubscription
        {
            UserId = userId,
            Endpoint = endpoint,
            P256dh = request.P256dh.Trim(),
            Auth = request.Auth.Trim(),
            UserAgent = userAgent,
        };

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        var kept = await _subscriptionRepository.GetAllAsync(s => s.UserId == userId,
                                                             projection: s => new WebPushSubscription { Id = s.Id, CreationDate = s.CreationDate },
                                                             cancellationToken: cancellationToken);

        if (kept.Count > WebPushSubscription.MaxPerUser)
        {
            var oldest = kept.OrderBy(s => s.CreationDate).ThenBy(s => s.Id).Take(kept.Count - WebPushSubscription.MaxPerUser).Select(s => s.Id).ToList();

            await _subscriptionRepository.ExecuteDeleteAsync(s => oldest.Contains(s.Id), cancellationToken: cancellationToken);
        }

        return Response<int>.Success(subscription.Id);
    }
}
