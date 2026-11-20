using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Storeasy.Infrastructure.Email;

/// <summary>
/// Sends e-mails through the Notifike notification API (<c>Storeasy:Email:Notifike</c>), the Milvasoft hosting
/// provider: <c>POST {BaseUrl}/notifications/send</c> with the <c>X-Api-Key</c> header. Uses the named HTTP client
/// <see cref="HttpClientName"/>. Throws when the API answers with a failure status, or with a success status whose
/// response envelope says it refused the message (<c>isSuccess: false</c>), so the outbox retries instead of dropping it.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
public sealed class NotifikeEmailSender(IHttpClientFactory httpClientFactory, EmailOptions options) : IEmailSender
{
    /// <summary>
    /// Name of the HTTP client used for Notifike calls.
    /// </summary>
    public const string HttpClientName = "notifike";

    /// <summary>
    /// Notifike channel number of e-mail.
    /// </summary>
    public const int EmailChannel = 1;

    private const string _apiKeyHeader = "X-Api-Key";

    // The body keeps the documented PascalCase names (Channel, Recipients, To...), not the web camelCase default.
    private static readonly JsonSerializerOptions _jsonOptions = new();

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly EmailOptions _options = options;

    /// <inheritdoc/>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var body = new
        {
            Channel = EmailChannel,
            Recipients = new[] { new { message.To, Name = message.ToName, message.Language } },
            message.Subject,
            Body = message.HtmlBody,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Notifike.BaseUrl.TrimEnd('/')}/notifications/send")
        {
            Content = JsonContent.Create(body, options: _jsonOptions),
        };

        request.Headers.TryAddWithoutValidation(_apiKeyHeader, _options.Notifike.ApiKey);

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);

        // The key is never part of the message; only the status is reported.
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Notifike answered {(int)response.StatusCode}.", null, response.StatusCode);

        var refusal = ReadRefusal(await response.Content.ReadAsStringAsync(cancellationToken));

        if (refusal != null)
            throw new HttpRequestException($"Notifike refused the message: {refusal}", null, response.StatusCode);
    }

    /// <summary>
    /// The refusal of a Notifike response envelope (<c>{ isSuccess, messages: [{ key, message }] }</c>): null when the
    /// body is empty, not JSON or does not say <c>isSuccess: false</c>; otherwise its message keys and texts.
    /// </summary>
    /// <param name="body"></param>
    public static string ReadRefusal(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var root = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

            if (!root.TryGetValue("isSuccess", out var success) || success.ValueKind != JsonValueKind.False)
                return null;

            var messages = root.TryGetValue("messages", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray()
                      .Where(m => m.ValueKind == JsonValueKind.Object)
                      .Select(m => string.Join(" ", m.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String && (p.NameEquals("key") || p.NameEquals("message") || p.NameEquals("Key") || p.NameEquals("Message"))).Select(p => p.Value.GetString())))
                      .Where(m => !string.IsNullOrWhiteSpace(m))
                      .ToList()
                : [];

            var text = messages.Count > 0 ? string.Join("; ", messages) : "no reason given";

            return text.Length > 300 ? text[..300] : text;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
