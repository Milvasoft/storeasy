using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.StoreJobs;

/// <summary>
/// The API's in-process store job runner (docs/urun-plani.md K4): <see cref="StoreJobOptions.Workers"/> loops, each
/// claiming and running one job at a time through a <see cref="StoreJobExecutor"/> in a fresh scope, and sleeping
/// <see cref="StoreJobOptions.PollIntervalMilliseconds"/> when the queue is empty. A worker survives any error of one
/// round (an unreachable database during startup included) and tries again after the pause.
/// </summary>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class StoreJobRunner(IServiceScopeFactory scopeFactory, StoreJobOptions options, TimeProvider timeProvider) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly StoreJobOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return Task.CompletedTask;

        return Task.WhenAll(Enumerable.Range(0, Math.Max(1, _options.Workers)).Select(_ => WorkAsync(stoppingToken)));
    }

    private async Task WorkAsync(CancellationToken stoppingToken)
    {
        // Let the startup (migrations) finish before the first claim.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            var ranJob = await RunOnceAsync(stoppingToken);

            if (ranJob)
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

    private async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        try
        {
            return await scope.ServiceProvider.GetRequiredService<StoreJobExecutor>().RunNextAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            scope.ServiceProvider.GetService<IMilvaLogger>()?.Warning(ex, "The store job runner could not claim or finish a job; it tries again after the poll interval.");

            return false;
        }
    }
}
