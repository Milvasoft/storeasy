using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Storeasy.Infrastructure.Notifications.WebPush;

/// <summary>
/// Sends one <see cref="WebPushMessage"/> to every subscribed browser of its users (scoped). Each push is encrypted to its
/// subscription (<see cref="WebPushCrypto.Encrypt"/>) and carries a VAPID authorization for the push service's origin. The
/// HTTP client connects only to public addresses (the outgoing webhook guard). A subscription the push service reports
/// gone (404, 410) is deleted; other failures are logged and the push is not retried.
/// </summary>
/// <param name="subscriptionRepository"></param>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class WebPushSender(IStoreasyRepositoryBase<WebPushSubscription> subscriptionRepository,
                                  IHttpClientFactory httpClientFactory,
                                  WebPushOptions options,
                                  TimeProvider timeProvider,
                                  IMilvaLogger logger)
{
    /// <summary>
    /// Name of the HTTP client that talks to the push services (tests replace its handler).
    /// </summary>
    public const string HttpClientName = "web-push";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IStoreasyRepositoryBase<WebPushSubscription> _subscriptionRepository = subscriptionRepository;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly WebPushOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMilvaLogger _logger = logger;

    /// <summary>
    /// Sends <paramref name="message"/>; returns how many browsers accepted it.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<int> SendAsync(WebPushMessage message, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return 0;

        var userIds = message.UserIds.Distinct().ToList();

        var subscriptions = await _subscriptionRepository.GetAllAsync(s => userIds.Contains(s.UserId),
                                                                      projection: s => new WebPushSubscription { Id = s.Id, Endpoint = s.Endpoint, P256dh = s.P256dh, Auth = s.Auth },
                                                                      cancellationToken: cancellationToken);

        if (subscriptions.Count == 0)
            return 0;

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            title = Trim(message.Title, 120),
            body = Trim(message.Body, 1000),
            url = message.Url,
            kind = message.Kind,
            notificationId = message.NotificationId,
        }, _json);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var now = _timeProvider.GetUtcNow();
        var delivered = new List<int>();
        var gone = new List<int>();

        foreach (var subscription in subscriptions)
        {
            var status = await PostAsync(client, subscription, payload, now, cancellationToken);

            if (status is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Accepted)
                delivered.Add(subscription.Id);
            else if (status is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                gone.Add(subscription.Id);
        }

        if (gone.Count > 0)
            await _subscriptionRepository.ExecuteDeleteAsync(s => gone.Contains(s.Id), cancellationToken: cancellationToken);

        if (delivered.Count > 0)
        {
            await _subscriptionRepository.ExecuteUpdateAsync(s => delivered.Contains(s.Id),
                                                             new SetPropertyBuilder<WebPushSubscription>().SetPropertyValue(s => s.LastSuccessDate, (DateTime?)now.UtcDateTime),
                                                             cancellationToken);
        }

        return delivered.Count;
    }

    private async Task<HttpStatusCode?> PostAsync(HttpClient client, WebPushSubscription subscription, byte[] payload, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var endpoint)
            || !WebPushKeys.TryDecode(subscription.P256dh, out var userAgentKey)
            || !WebPushKeys.TryDecode(subscription.Auth, out var authSecret))
            return HttpStatusCode.Gone;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

            request.Content = new ByteArrayContent(WebPushCrypto.Encrypt(payload, userAgentKey, authSecret));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");
            request.Headers.TryAddWithoutValidation("TTL", Math.Max(0, _options.TimeToLiveSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation("Urgency", "normal");
            request.Headers.TryAddWithoutValidation("Authorization", WebPushCrypto.CreateVapidAuthorization(endpoint, _options.Subject, _options.PublicKey, _options.PrivateKey, now));

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode && response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone))
                _logger.Warning("Push service {Host} answered {Status}.", endpoint.Host, (int)response.StatusCode);

            return response.StatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException or System.Security.Cryptography.CryptographicException)
        {
            _logger.Warning("Push to {Host} failed: {Error}", endpoint.Host, SecretRedactor.Redact(ex.Message));

            return ex is ArgumentException or System.Security.Cryptography.CryptographicException ? HttpStatusCode.Gone : null;
        }
    }

    private static string Trim(string value, int max) => value?.Length > max ? value[..max] : value;
}
