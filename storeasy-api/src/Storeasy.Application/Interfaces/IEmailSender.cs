namespace Storeasy.Application.Interfaces;

/// <summary>
/// Sends one e-mail through the provider selected by <c>Storeasy:Email:Provider</c> (SMTP, Notifike, or the
/// Development only log sender). Implementations throw when the provider refuses or cannot be reached; callers that
/// must not fail on mail (registration, password reset) catch and log.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="message"/>.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// One e-mail to one recipient.
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="ToName">Recipient display name; may be empty.</param>
/// <param name="Language">Culture the text is written in (e.g. tr-TR).</param>
/// <param name="Subject">Subject line.</param>
/// <param name="HtmlBody">HTML body.</param>
/// <param name="ActionUrl">The link the e-mail carries, if any. Only the Development log sender prints it.</param>
public sealed record EmailMessage(string To, string ToName, string Language, string Subject, string HtmlBody, string ActionUrl = null);
