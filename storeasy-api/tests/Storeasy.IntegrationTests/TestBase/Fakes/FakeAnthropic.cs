using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The Anthropic API as the AI features (the real SDK) see it: <c>GET /v1/models/{id}</c> for the key check and
/// <c>POST /v1/messages</c> for the reply drafts and the pre-reviews (the beta path adds <c>?beta=true</c>). No test ever reaches Anthropic or needs a real key. The behaviour depends on the
/// key, so tests that run side by side never share it: a key containing <see cref="RejectedMarker"/> gets 401, one with
/// <see cref="OfflineMarker"/> cannot connect, and <see cref="Replies"/> sets the answer of the messages call per key.
/// </summary>
public sealed class FakeAnthropic : HttpMessageHandler
{
    /// <summary>Prefix of every key the tests use (shaped like an Anthropic key, but not one).</summary>
    public const string KeyPrefix = "sk-ant-test-";

    /// <summary>A key with this part is rejected (401).</summary>
    public const string RejectedMarker = "rejected";

    /// <summary>A key with this part cannot reach Anthropic (connection error).</summary>
    public const string OfflineMarker = "offline";

    /// <summary>Default draft of the messages call.</summary>
    public const string DefaultReply = "Thank you for your review. The team will look into the reminders.";

    /// <summary>Answer of the messages call by key: (status, stop reason, text).</summary>
    public ConcurrentDictionary<string, (HttpStatusCode Status, string StopReason, string Text)> Replies { get; } = new();

    /// <summary>Requests received: the bearer key, the path, the body and the <c>anthropic-beta</c> header.</summary>
    public ConcurrentQueue<(string ApiKey, string Path, string Body, string Beta)> Requests { get; } = new();

    /// <summary>A unique test key for <paramref name="suffix"/>, optionally with a marker.</summary>
    public static string Key(string suffix, string marker = "valid") => $"{KeyPrefix}{marker}-{suffix}-000000000000";

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = request.Headers.Authorization?.Parameter ?? string.Empty;
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath;

        var beta = request.Headers.TryGetValues("anthropic-beta", out var betas) ? string.Join(",", betas) : null;

        Requests.Enqueue((apiKey, path, body, beta));

        if (apiKey.Contains(OfflineMarker, StringComparison.Ordinal))
            throw new HttpRequestException("No connection could be made (test).");

        if (!apiKey.StartsWith(KeyPrefix, StringComparison.Ordinal) || apiKey.Contains(RejectedMarker, StringComparison.Ordinal))
            return Error(HttpStatusCode.Unauthorized, "authentication_error");

        if (request.Method == HttpMethod.Get && path.StartsWith("/v1/models/", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, new { type = "model", id = path["/v1/models/".Length..], display_name = "Claude", created_at = "2026-10-01T00:00:00Z" });

        if (request.Method == HttpMethod.Post && path == "/v1/messages")
        {
            var (status, stopReason, text) = Replies.TryGetValue(apiKey, out var reply) ? reply : (HttpStatusCode.OK, "end_turn", DefaultReply);

            if (status != HttpStatusCode.OK)
                return Error(status, "error");

            return Json(HttpStatusCode.OK, new
            {
                id = "msg_test",
                type = "message",
                role = "assistant",
                model = "claude-haiku-5-5",
                content = new object[] { new { type = "text", text } },
                stop_reason = stopReason,
                stop_sequence = (string)null,
                usage = new { input_tokens = 400, output_tokens = 60 },
            });
        }

        return Error(HttpStatusCode.NotFound, "not_found_error");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Error(HttpStatusCode status, string type)
    {
        var response = Json(status, new { type = "error", error = new { type, message = "raw provider message that must not leak" } });

        response.Headers.Add("retry-after", "0");

        return response;
    }
}
