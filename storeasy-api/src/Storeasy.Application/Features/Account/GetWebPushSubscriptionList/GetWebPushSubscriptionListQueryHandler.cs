using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.GetWebPushSubscriptionList;

/// <summary>
/// Handles <see cref="GetWebPushSubscriptionListQuery"/>.
/// </summary>
/// <param name="subscriptionRepository"></param>
/// <param name="currentUser"></param>
public class GetWebPushSubscriptionListQueryHandler(IStoreasyRepositoryBase<WebPushSubscription> subscriptionRepository, ICurrentUser currentUser) : IInterceptable, IQueryHandler<GetWebPushSubscriptionListQuery, List<WebPushSubscriptionDto>>
{
    private readonly IStoreasyRepositoryBase<WebPushSubscription> _subscriptionRepository = subscriptionRepository;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public async Task<Response<List<WebPushSubscriptionDto>>> Handle(GetWebPushSubscriptionListQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();

        var subscriptions = await _subscriptionRepository.GetAllAsync(s => s.UserId == userId,
                                                                      projection: s => new WebPushSubscription { Id = s.Id, Endpoint = s.Endpoint, UserAgent = s.UserAgent, CreationDate = s.CreationDate, LastSuccessDate = s.LastSuccessDate },
                                                                      cancellationToken: cancellationToken);

        return Response<List<WebPushSubscriptionDto>>.Success([.. subscriptions.OrderByDescending(s => s.CreationDate).Select(s => new WebPushSubscriptionDto
        {
            Id = s.Id,
            PushServiceHost = Uri.TryCreate(s.Endpoint, UriKind.Absolute, out var uri) ? uri.Host : null,
            UserAgent = s.UserAgent,
            CreationDate = s.CreationDate,
            LastSuccessDate = s.LastSuccessDate,
        })]);
    }
}
