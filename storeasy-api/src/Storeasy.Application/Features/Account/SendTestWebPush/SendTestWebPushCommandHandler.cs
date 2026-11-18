using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Application.Features.Account.SendTestWebPush;

/// <summary>
/// Handles <see cref="SendTestWebPushCommand"/>.
/// </summary>
/// <param name="SubscriptionRepository"></param>
/// <param name="WebPushQueue"></param>
/// <param name="CurrentUser"></param>
/// <param name="Localizer"></param>
/// <param name="Options"></param>
/// <param name="AppOptions"></param>
public record SendTestWebPushCommandHandler(IStoreasyRepositoryBase<WebPushSubscription> SubscriptionRepository,
                                            IWebPushQueue WebPushQueue,
                                            ICurrentUser CurrentUser,
                                            IMilvaLocalizer Localizer,
                                            WebPushOptions Options,
                                            AppOptions AppOptions) : IInterceptable, ICommandHandler<SendTestWebPushCommand, int>
{
    private readonly IStoreasyRepositoryBase<WebPushSubscription> _subscriptionRepository = SubscriptionRepository;
    private readonly IWebPushQueue _webPushQueue = WebPushQueue;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly WebPushOptions _options = Options;
    private readonly AppOptions _appOptions = AppOptions;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SendTestWebPushCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Response<int>.Error(0, MessageKey.WebPushDisabled);

        var userId = _currentUser.GetRequiredId();

        var subscriptions = await _subscriptionRepository.GetAllAsync(s => s.UserId == userId, projection: s => new WebPushSubscription { Id = s.Id }, cancellationToken: cancellationToken);

        if (subscriptions.Count == 0)
            return Response<int>.Error(0, MessageKey.WebPushSubscriptionNotFound);

        _webPushQueue.TryEnqueue(new WebPushMessage([userId], _localizer[MessageKey.WebPushTestTitle], _localizer[MessageKey.WebPushTestBody], _appOptions.BaseUrl, "Test"));

        return Response<int>.Success(subscriptions.Count);
    }
}
