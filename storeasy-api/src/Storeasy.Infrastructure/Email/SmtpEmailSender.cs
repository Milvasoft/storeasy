using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Storeasy.Infrastructure.Email;

/// <summary>
/// Sends e-mails through an SMTP server (<c>Storeasy:Email:Smtp</c>), the self-host provider. Throws when the server
/// refuses or cannot be reached.
/// </summary>
/// <param name="options"></param>
public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    private readonly EmailOptions _options = options;

    /// <inheritdoc/>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = _options.Smtp;

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = string.IsNullOrWhiteSpace(smtp.UserName) ? null : new NetworkCredential(smtp.UserName, smtp.Password),
        };

        using var mail = new MailMessage(new MailAddress(smtp.From, _options.FromName), new MailAddress(message.To, message.ToName))
        {
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.HtmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true,
        };

        await client.SendMailAsync(mail, cancellationToken);
    }
}
