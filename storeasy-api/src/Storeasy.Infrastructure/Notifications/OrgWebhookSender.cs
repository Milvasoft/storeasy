using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Notifications;
using System.Diagnostics;
using System.Text;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// Default <see cref="IOrgWebhookSender"/>. A batch is claimed with a lease id in one update; the attempts are made one
/// by one and their results written back in one update. Backoff: <see cref="OrgWebhookOptions.RetryBaseDelaySeconds"/>
/// doubled per attempt, at most an hour; a 410 Gone stops the retries.
/// </summary>
/// <param name="deliveryRepository"></param>
/// <param name="webhookRepository"></param>
/// <param name="httpClientFactory"></param>
/// <param name="urlGuard"></param>
/// <param name="secretProtector"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class OrgWebhookSender(IStoreasyRepositoryBase<WebhookDelivery> deliveryRepository,
                                     IStoreasyRepositoryBase<OrganizationWebhook> webhookRepository,
                                     IHttpClientFactory httpClientFactory,
                                     IWebhookUrlGuard urlGuard,
                                     ISecretProtector secretProtector,
                                     OrgWebhookOptions options,
                                     TimeProvider timeProvider,
                                     IMilvaLogger logger) : IOrgWebhookSender
{
    /// <summary>
    /// Name of the HTTP client of the deliveries (no redirects, connections to private addresses refused).
    /// </summary>
    public const string HttpClientName = "org-webhooks";

    private const int _batchSize = 20;
    private const int _leaseSeconds = 120;
    private const string _userAgent = "storeasy-webhooks/1.0";

    private readonly IStoreasyRepositoryBase<WebhookDelivery> _deliveryRepository = deliveryRepository;
    private readonly IStoreasyRepositoryBase<OrganizationWebhook> _webhookRepository = webhookRepository;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IWebhookUrlGuard _urlGuard = urlGuard;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly OrgWebhookOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMilvaLogger _logger = logger;

    /// <inheritdoc/>
    public async Task<int> SendDueAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var due = await _deliveryRepository.GetSomeAsync(_batchSize,
                                                         d => (d.Status == WebhookDeliveryStatus.Pending || d.Status == WebhookDeliveryStatus.Retrying) && d.NextAttemptDate <= now && (d.LockedUntil == null || d.LockedUntil < now),
                                                         projection: d => new WebhookDelivery { Id = d.Id },
                                                         cancellationToken: cancellationToken);

        if (due.Count == 0)
            return 0;

        var ids = due.Select(d => d.Id).ToList();

        return (await ClaimAndSendAsync(d => ids.Contains(d.Id) && (d.LockedUntil == null || d.LockedUntil < now), now, cancellationToken)).Count;
    }

    /// <inheritdoc/>
    public async Task<WebhookDelivery> SendNowAsync(long deliveryId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var sent = await ClaimAndSendAsync(d => d.Id == deliveryId && (d.LockedUntil == null || d.LockedUntil < now), now, cancellationToken);

        return sent.FirstOrDefault();
    }

    private async Task<List<WebhookDelivery>> ClaimAndSendAsync(System.Linq.Expressions.Expression<Func<WebhookDelivery, bool>> condition, DateTime now, CancellationToken cancellationToken)
    {
        var leaseId = Guid.NewGuid().ToString("N");

        await _deliveryRepository.ExecuteUpdateAsync(condition,
                                                     new SetPropertyBuilder<WebhookDelivery>().SetPropertyValue(d => d.LeaseId, leaseId)
                                                                                              .SetPropertyValue(d => d.LockedUntil, (DateTime?)now.AddSeconds(_leaseSeconds)),
                                                     cancellationToken);

        var claimed = await _deliveryRepository.GetAllAsync(d => d.LeaseId == leaseId,
                                                            projection: d => new WebhookDelivery
                                                            {
                                                                Id = d.Id,
                                                                OrganizationId = d.OrganizationId,
                                                                OrganizationWebhookId = d.OrganizationWebhookId,
                                                                EventType = d.EventType,
                                                                EventId = d.EventId,
                                                                Payload = d.Payload,
                                                                Status = d.Status,
                                                                Attempt = d.Attempt,
                                                                MaxAttempts = d.MaxAttempts,
                                                                IsTest = d.IsTest,
                                                                CreationDate = d.CreationDate,
                                                            },
                                                            cancellationToken: cancellationToken);

        if (claimed.Count == 0)
            return claimed;

        var webhookIds = claimed.Select(d => d.OrganizationWebhookId).Distinct().ToList();

        var webhooks = (await _webhookRepository.GetAllAsync(w => webhookIds.Contains(w.Id),
                                                             projection: w => new OrganizationWebhook { Id = w.Id, OrganizationId = w.OrganizationId, Url = w.Url, IsActive = w.IsActive, EncryptedSecret = w.EncryptedSecret, EncryptedDataKey = w.EncryptedDataKey, KeyVersion = w.KeyVersion },
                                                             cancellationToken: cancellationToken)).ToDictionary(w => w.Id);

        foreach (var delivery in claimed)
            await AttemptAsync(delivery, webhooks.GetValueOrDefault(delivery.OrganizationWebhookId), cancellationToken);

        await _deliveryRepository.UpdateAsync(claimed, cancellationToken, d => d.Status, d => d.Attempt, d => d.NextAttemptDate, d => d.LockedUntil, d => d.LeaseId, d => d.ResponseStatusCode, d => d.ResponseBody, d => d.Error, d => d.DurationMilliseconds, d => d.LastAttemptDate);

        var touched = claimed.GroupBy(d => d.OrganizationWebhookId)
                             .Where(g => webhooks.ContainsKey(g.Key))
                             .Select(g => new OrganizationWebhook { Id = g.Key, LastDeliveryDate = g.Max(d => d.LastAttemptDate), LastDeliveryStatus = g.OrderByDescending(d => d.LastAttemptDate).First().Status })
                             .ToList();

        if (touched.Count > 0)
            await _webhookRepository.UpdateAsync(touched, cancellationToken, w => w.LastDeliveryDate, w => w.LastDeliveryStatus);

        return claimed;
    }

    private async Task AttemptAsync(WebhookDelivery delivery, OrganizationWebhook webhook, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        delivery.Attempt++;
        delivery.LastAttemptDate = now;
        delivery.LockedUntil = null;
        delivery.LeaseId = null;

        if (webhook == null || !webhook.IsActive)
        {
            Finish(delivery, WebhookDeliveryStatus.Failed, null, null, "The subscription is removed or switched off.", 0);
            return;
        }

        var problem = await _urlGuard.CheckAsync(webhook.Url, cancellationToken);

        if (problem != null)
        {
            Finish(delivery, WebhookDeliveryStatus.Failed, null, null, $"The address was refused ({problem}).", 0);
            return;
        }

        string secret;

        try
        {
            secret = Encoding.UTF8.GetString(_secretProtector.Unprotect(new ProtectedSecret(webhook.EncryptedSecret, webhook.EncryptedDataKey, webhook.KeyVersion), SecretScopes.OrganizationWebhook(webhook.OrganizationId)));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or ArgumentException)
        {
            Finish(delivery, WebhookDeliveryStatus.Failed, null, null, "The signing secret could not be opened.", 0);
            return;
        }

        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var stopwatch = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
        {
            Content = new StringContent(delivery.Payload ?? "{}", Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        request.Headers.TryAddWithoutValidation(WebhookSignatures.EventTypeHeader, delivery.EventType);
        request.Headers.TryAddWithoutValidation(WebhookSignatures.EventIdHeader, delivery.EventId);
        request.Headers.TryAddWithoutValidation(WebhookSignatures.TimestampHeader, timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation(WebhookSignatures.SignatureHeader, WebhookSignatures.ComputeStoreasy(timestamp, delivery.Payload ?? "{}", secret));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, timeout.Token);

            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var status = (int)response.StatusCode;
            var duration = (int)stopwatch.ElapsedMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                Finish(delivery, WebhookDeliveryStatus.Succeeded, status, body, null, duration);
                return;
            }

            Retry(delivery, status, body, $"The receiver answered {status}.", duration, final: status == 410);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            Retry(delivery, null, null, ex is HttpRequestException ? $"Unreachable: {ex.Message}" : "The receiver did not answer in time.", (int)stopwatch.ElapsedMilliseconds, final: false);
        }
    }

    private void Retry(WebhookDelivery delivery, int? status, string body, string error, int duration, bool final)
    {
        if (final || delivery.IsTest || delivery.Attempt >= delivery.MaxAttempts)
        {
            Finish(delivery, WebhookDeliveryStatus.Failed, status, body, error, duration);
            return;
        }

        var delaySeconds = Math.Min(3600, Math.Max(0, _options.RetryBaseDelaySeconds) * Math.Pow(2, delivery.Attempt - 1));

        Finish(delivery, WebhookDeliveryStatus.Retrying, status, body, error, duration);
        delivery.NextAttemptDate = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(delaySeconds);
    }

    private void Finish(WebhookDelivery delivery, WebhookDeliveryStatus status, int? statusCode, string body, string error, int duration)
    {
        delivery.Status = status;
        delivery.ResponseStatusCode = statusCode;
        delivery.ResponseBody = body == null ? null : SecretRedactor.RedactAndTrim(body, WebhookDelivery.ResponseMaxLength);
        delivery.Error = error == null ? null : SecretRedactor.RedactAndTrim(error, WebhookDelivery.ResponseMaxLength);
        delivery.DurationMilliseconds = duration;
        delivery.NextAttemptDate = null;

        if (status == WebhookDeliveryStatus.Failed)
            _logger.Warning("Webhook delivery {DeliveryId} failed: {Error}", delivery.Id, delivery.Error);
    }
}
