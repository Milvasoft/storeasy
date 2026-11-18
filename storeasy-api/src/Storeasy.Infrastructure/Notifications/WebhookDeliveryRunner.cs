using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// Posts the due outgoing webhook deliveries in the background (<see cref="IOrgWebhookSender.SendDueAsync"/>), pausing
/// <see cref="OrgWebhookOptions.PollIntervalMilliseconds"/> when nothing is due. Survives any error of one round.
/// </summary>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class WebhookDeliveryRunner(IServiceScopeFactory scopeFactory, OrgWebhookOptions options, TimeProvider timeProvider) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly OrgWebhookOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            var sent = await RunOnceAsync(stoppingToken);

            if (sent > 0)
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

    private async Task<int> RunOnceAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        try
        {
            return await scope.ServiceProvider.GetRequiredService<IOrgWebhookSender>().SendDueAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            scope.ServiceProvider.GetService<IMilvaLogger>()?.Warning(ex, "The webhook delivery runner failed a round; it tries again after the poll interval.");

            return 0;
        }
    }
}
