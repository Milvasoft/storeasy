using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Milvasoft.Caching.Redis.Accessor;
using Storeasy.Application.Dtos.SettingsDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Persistence.Context;
using System.Diagnostics;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="ISystemStatusProbe"/>. Each dependency check is timed and limited to <see cref="CheckTimeout"/>; a
/// failure is reported (redacted) instead of thrown. The queue numbers are counts over all organizations (raw counts on the
/// database context, the only cross organization read of the API).
/// </summary>
/// <param name="dbContext"></param>
/// <param name="cache"></param>
/// <param name="fileStorage"></param>
/// <param name="emailOptions"></param>
/// <param name="encryptionOptions"></param>
/// <param name="googleAuthOptions"></param>
/// <param name="webPushOptions"></param>
/// <param name="buildOptions"></param>
/// <param name="hostEnvironment"></param>
/// <param name="timeProvider"></param>
public sealed class SystemStatusProbe(StoreasyDbContext dbContext,
                                      IRedisAccessor cache,
                                      IFileStorage fileStorage,
                                      EmailOptions emailOptions,
                                      EncryptionOptions encryptionOptions,
                                      GoogleAuthOptions googleAuthOptions,
                                      WebPushOptions webPushOptions,
                                      BuildOptions buildOptions,
                                      IHostEnvironment hostEnvironment,
                                      TimeProvider timeProvider) : ISystemStatusProbe
{
    /// <summary>
    /// Longest wait for one dependency.
    /// </summary>
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

    private const string _storageProbeKey = "system/status-probe";

    private readonly StoreasyDbContext _dbContext = dbContext;
    private readonly IRedisAccessor _cache = cache;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly EmailOptions _emailOptions = emailOptions;
    private readonly EncryptionOptions _encryptionOptions = encryptionOptions;
    private readonly GoogleAuthOptions _googleAuthOptions = googleAuthOptions;
    private readonly WebPushOptions _webPushOptions = webPushOptions;
    private readonly BuildOptions _buildOptions = buildOptions;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<SystemStatusDto> CheckAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var database = await TimedAsync(true, async token => await _dbContext.Database.CanConnectAsync(token), cancellationToken);
        var redisConnected = _cache.IsConnected();
        var redis = await TimedAsync(redisConnected, async _ => (await _cache.GetDatabase().PingAsync()) < CheckTimeout, cancellationToken);
        var storage = await TimedAsync(_fileStorage.IsConfigured, async token =>
        {
            await _fileStorage.GetObjectInfoAsync(_storageProbeKey, token);

            return true;
        }, cancellationToken);
        var dayAgo = now.AddDays(-1);

        return new SystemStatusDto
        {
            Version = _buildOptions.Version,
            Commit = _buildOptions.ResolvedCommit,
            Environment = _hostEnvironment.EnvironmentName,
            CheckedAt = now,
            Database = database,
            Redis = redis,
            Storage = storage,
            Mail = new DependencyStatusDto { Configured = _emailOptions.FindMissingSetting() == null, Provider = _emailOptions.Provider.ToString() },
            SignalRBackplane = redisConnected ? "Redis" : "Local",
            EncryptionKeyConfigured = !string.IsNullOrWhiteSpace(_encryptionOptions.MasterKey),
            GoogleSignInConfigured = _googleAuthOptions.Enabled,
            WebPushConfigured = _webPushOptions.Enabled,
            Jobs = database.Healthy != true ? new JobQueueStatusDto() : new JobQueueStatusDto
            {
                Queued = await _dbContext.StoreJobs.CountAsync(j => j.Status == StoreJobStatus.Queued, cancellationToken),
                Running = await _dbContext.StoreJobs.CountAsync(j => j.Status == StoreJobStatus.Running, cancellationToken),
                SucceededLastDay = await _dbContext.StoreJobs.CountAsync(j => j.Status == StoreJobStatus.Succeeded && j.EndDate >= dayAgo, cancellationToken),
                FailedLastDay = await _dbContext.StoreJobs.CountAsync(j => j.Status == StoreJobStatus.Failed && j.EndDate >= dayAgo, cancellationToken),
                OldestQueuedDate = await _dbContext.StoreJobs.Where(j => j.Status == StoreJobStatus.Queued).OrderBy(j => j.NextAttemptDate).Select(j => (DateTime?)j.NextAttemptDate).FirstOrDefaultAsync(cancellationToken),
            },
            PendingEmails = database.Healthy != true ? 0 : await _dbContext.EmailOutbox.CountAsync(cancellationToken),
        };
    }

    private static async Task<DependencyStatusDto> TimedAsync(bool configured, Func<CancellationToken, Task<bool>> check, CancellationToken cancellationToken)
    {
        var status = new DependencyStatusDto { Configured = configured };

        if (!configured)
            return status;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CheckTimeout);

        var watch = Stopwatch.StartNew();

        try
        {
            status.Healthy = await check(timeout.Token).WaitAsync(timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            status.Healthy = false;
            status.Error = SecretRedactor.RedactAndTrim(ex.Message, 200);
        }

        status.LatencyMs = watch.ElapsedMilliseconds;

        return status;
    }
}
