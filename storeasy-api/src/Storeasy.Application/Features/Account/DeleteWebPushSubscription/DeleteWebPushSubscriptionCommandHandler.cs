using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Storeasy.Application.Features.Account.DeleteWebPushSubscription;

/// <summary>
/// Handles <see cref="DeleteWebPushSubscriptionCommand"/>. Another user's subscription is reported as not found.
/// </summary>
/// <param name="SubscriptionRepository"></param>
/// <param name="CurrentUser"></param>
public record DeleteWebPushSubscriptionCommandHandler(IStoreasyRepositoryBase<WebPushSubscription> SubscriptionRepository, ICurrentUser CurrentUser) : IInterceptable, ICommandHandler<DeleteWebPushSubscriptionCommand, int>
{
    private readonly IStoreasyRepositoryBase<WebPushSubscription> _subscriptionRepository = SubscriptionRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteWebPushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();
        var endpoint = request.Endpoint?.Trim();

        var id = await _subscriptionRepository.GetFirstOrDefaultAsync(s => s.UserId == userId && ((request.Id.HasValue && s.Id == request.Id.Value) || (endpoint != null && s.Endpoint == endpoint)),
                                                                       projection: s => (int?)s.Id,
                                                                       cancellationToken: cancellationToken);

        if (id == null)
            return Response<int>.Error(0, MessageKey.WebPushSubscriptionNotFound);

        await _subscriptionRepository.ExecuteDeleteAsync(s => s.Id == id.Value, cancellationToken: cancellationToken);

        return Response<int>.Success(id.Value);
    }
}
