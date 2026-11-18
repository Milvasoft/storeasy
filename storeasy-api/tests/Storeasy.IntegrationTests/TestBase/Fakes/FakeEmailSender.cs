using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;
using System.Web;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Replaces the e-mail provider: records every e-mail instead of sending it, so a test can read the link (and its token)
/// a user would have received.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    /// <summary>
    /// E-mails in the order they were sent.
    /// </summary>
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    /// <summary>
    /// When set, every send throws (a provider outage).
    /// </summary>
    public bool Fail { get; set; }

    /// <inheritdoc/>
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
            throw new HttpRequestException("Provider down.");

        Sent.Enqueue(message);

        return Task.CompletedTask;
    }

    /// <summary>
    /// The e-mails sent to <paramref name="email"/>, oldest first.
    /// </summary>
    public List<EmailMessage> To(string email) => [.. Sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// The token of the newest e-mail to <paramref name="email"/> whose link points at <paramref name="path"/>, or null.
    /// </summary>
    public string LastToken(string email, string path)
    {
        var link = To(email).LastOrDefault(m => m.ActionUrl?.Contains($"/{path}?") == true)?.ActionUrl;

        return link == null ? null : HttpUtility.ParseQueryString(new Uri(link).Query)["token"];
    }
}
