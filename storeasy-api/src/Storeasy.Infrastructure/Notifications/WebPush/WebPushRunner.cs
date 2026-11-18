using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Milvasoft.Core.Abstractions;

namespace Storeasy.Infrastructure.Notifications.WebPush;

/// <summary>
/// Reads <see cref="WebPushQueue"/> and sends each push through a fresh scoped <see cref="WebPushSender"/>. A failure of
/// one push is logged and never stops the loop.
/// </summary>
/// <param name="queue"></param>
/// <param name="scopeFactory"></param>
public sealed class WebPushRunner(WebPushQueue queue, IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly WebPushQueue _queue = queue;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                try
                {
                    await scope.ServiceProvider.GetRequiredService<WebPushSender>().SendAsync(message, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    scope.ServiceProvider.GetService<IMilvaLogger>()?.Warning(ex, "A web push could not be sent.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown: queued pushes are dropped.
        }
    }
}
