using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Email;

/// <summary>
/// The background e-mail sender: one loop that sends outbox e-mails one by one through a fresh scoped
/// <see cref="EmailOutboxSender"/> and sleeps <see cref="EmailOutboxOptions.PollIntervalMilliseconds"/> when the outbox is
/// empty. Survives any error of one round (an unreachable database during startup included).
/// </summary>
/// <param name="scopeFactory"></param>
/// <param name="emailOptions"></param>
/// <param name="timeProvider"></param>
public sealed class EmailOutboxRunner(IServiceScopeFactory scopeFactory, EmailOptions emailOptions, TimeProvider timeProvider) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly EmailOutboxOptions _options = emailOptions.Outbox ?? new EmailOutboxOptions();
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        // Let the startup (migrations) finish before the first claim.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await SendOnceAsync(stoppingToken))
                continue;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(10, _options.PollIntervalMilliseconds)), _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<bool> SendOnceAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        try
        {
            return await scope.ServiceProvider.GetRequiredService<EmailOutboxSender>().SendNextAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            scope.ServiceProvider.GetService<IMilvaLogger>()?.Warning(ex, "The e-mail outbox sender could not claim or finish an e-mail; it tries again after the poll interval.");

            return false;
        }
    }
}
