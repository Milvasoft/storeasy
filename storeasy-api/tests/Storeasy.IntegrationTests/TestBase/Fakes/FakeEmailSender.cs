using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;
using System.Web;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Replaces the e-mail provider: records every e-mail instead of sending it, so a test can read the link (and its token)
/// a user would have received. E-mails leave through the outbox's background sender, so a test waits for them
/// (<see cref="WaitForAsync"/>, <see cref="LastTokenAsync"/>) instead of reading right after the request.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

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

    /// <summary>
    /// Waits until <paramref name="count"/> e-mails to <paramref name="email"/> were sent and returns them; throws on
    /// timeout.
    /// </summary>
    public async Task<List<EmailMessage>> WaitForAsync(string email, int count = 1)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (To(email).Count < count)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"{count} e-mail(s) to {email} were expected.");

            await Task.Delay(50);
        }

        return To(email);
    }

    /// <summary>
    /// Waits for an e-mail to <paramref name="email"/> whose link points at <paramref name="path"/> and is not
    /// <paramref name="previous"/>, then returns its token.
    /// </summary>
    public async Task<string> LastTokenAsync(string email, string path, string previous = null)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var token = LastToken(email, path);

            if (token != null && token != previous)
                return token;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"No {path} e-mail to {email} arrived.");

            await Task.Delay(50);
        }
    }
}
