using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Storeasy.Infrastructure.Email;

/// <summary>
/// Sends e-mails through the Notifike notification API (<c>Storeasy:Email:Notifike</c>), the Milvasoft hosting
/// provider: <c>POST {BaseUrl}/notifications/send</c> with the <c>X-Api-Key</c> header. Uses the named HTTP client
/// <see cref="HttpClientName"/>. Throws when the API answers with a failure status.
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
    }
}
