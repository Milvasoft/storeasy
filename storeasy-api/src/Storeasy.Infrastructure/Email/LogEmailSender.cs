using Microsoft.Extensions.Hosting;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.Email;

/// <summary>
/// Sends nothing: writes that an e-mail would have been sent, and in Development the link it carries, so a developer can
/// register and reset passwords without a mail server. The startup refuses this provider outside Development; the link
/// is still only written in Development as a second guard.
/// </summary>
/// <param name="logger"></param>
/// <param name="environment"></param>
public sealed class LogEmailSender(IMilvaLogger logger, IHostEnvironment environment) : IEmailSender
{
    private readonly IMilvaLogger _logger = logger;
    private readonly IHostEnvironment _environment = environment;

    /// <inheritdoc/>
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment())
            _logger.Information("E-mail '{Subject}' to {To} (log provider, not sent). Link: {ActionUrl}", message.Subject, message.To, message.ActionUrl);
        else
            _logger.Information("E-mail '{Subject}' to {To} (log provider, not sent).", message.Subject, message.To);

        return Task.CompletedTask;
    }
}
