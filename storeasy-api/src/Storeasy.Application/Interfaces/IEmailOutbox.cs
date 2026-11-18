namespace Storeasy.Application.Interfaces;

/// <summary>
/// The e-mail outbox: e-mails are written here on the request path and delivered by a background sender through
/// <see cref="IEmailSender"/>, with retries. The body is sealed with the secret protector (it may carry a one time link),
/// and a row is deleted once it is sent or its attempts are used up.
/// </summary>
public interface IEmailOutbox
{
    /// <summary>
    /// Queues ready e-mails.
    /// </summary>
    /// <param name="messages"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task EnqueueAsync(IReadOnlyList<EmailMessage> messages, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a password reset request for an address in <paramref name="language"/>; the sender resolves the account.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="language"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task EnqueuePasswordResetAsync(string email, string language, CancellationToken cancellationToken);
}
