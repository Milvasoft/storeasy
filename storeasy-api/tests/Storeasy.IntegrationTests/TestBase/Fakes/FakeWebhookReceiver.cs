using System.Collections.Concurrent;
using System.Net;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The receivers of outgoing webhooks: records each delivery (headers and body) and answers with the status set for its
/// host (200 by default).
/// </summary>
public sealed class FakeWebhookReceiver
{
    /// <summary>Deliveries received.</summary>
    public ConcurrentQueue<ReceivedWebhook> Received { get; } = new();

    /// <summary>Status by host.</summary>
    public ConcurrentDictionary<string, HttpStatusCode> Statuses { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A handler for one HTTP client (the factory disposes handlers; the recorder stays).
    /// </summary>
    public HttpMessageHandler CreateHandler() => new Handler(this);

    /// <summary>The deliveries of one host.</summary>
    public List<ReceivedWebhook> For(string host) => [.. Received.Where(r => string.Equals(r.Host, host, StringComparison.OrdinalIgnoreCase))];

    private sealed class Handler(FakeWebhookReceiver receiver) : HttpMessageHandler
    {
        private readonly FakeWebhookReceiver _receiver = receiver;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

            _receiver.Received.Enqueue(new ReceivedWebhook(request.RequestUri.Host, request.RequestUri.ToString(), headers, body));

            var status = _receiver.Statuses.TryGetValue(request.RequestUri.Host, out var configured) ? configured : HttpStatusCode.OK;

            return new HttpResponseMessage(status) { Content = new StringContent(status == HttpStatusCode.OK ? "ok" : "nope") };
        }
    }
}

/// <summary>
/// One received delivery.
/// </summary>
/// <param name="Host">Host.</param>
/// <param name="Url">Address.</param>
/// <param name="Headers">Headers.</param>
/// <param name="Body">Body.</param>
public sealed record ReceivedWebhook(string Host, string Url, Dictionary<string, string> Headers, string Body);
