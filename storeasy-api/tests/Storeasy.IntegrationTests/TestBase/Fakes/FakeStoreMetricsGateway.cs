using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The store page metrics reads without a store: App Store Connect analytics report requests and reports, the Google Play
/// statistics bucket and the Microsoft Store analytics API. Keyed by the store's app id (tests use unique ids). Records
/// every report request created and every read with its range; never calls anything.
/// </summary>
public sealed class FakeStoreMetricsGateway : IStoreMetricsGateway
{
    private int _sequence;

    /// <summary>App Store report requests by app id (an existing one is found, not created).</summary>
    public ConcurrentDictionary<string, AppleReportRequest> AppleRequests { get; } = new();

    /// <summary>App Store report requests created: app ids.</summary>
    public ConcurrentQueue<string> CreatedRequests { get; } = new();

    /// <summary>App Store batches by request id; a request without one has no reports yet.</summary>
    public ConcurrentDictionary<string, StoreMetricBatch> AppleBatches { get; } = new();

    /// <summary>Play rows by package name.</summary>
    public ConcurrentDictionary<string, List<StoreMetricRow>> PlayRows { get; } = new();

    /// <summary>Microsoft rows by product id.</summary>
    public ConcurrentDictionary<string, List<StoreMetricRow>> MicrosoftRows { get; } = new();

    /// <summary>Failures by app id (e.g. a 403 for a refused bucket).</summary>
    public ConcurrentDictionary<string, StoreCallException> Failures { get; } = new();

    /// <summary>Reads made: (app id, bucket or request id, from, to).</summary>
    public ConcurrentQueue<(string AppId, string Key, DateOnly? From, DateOnly? To)> Reads { get; } = new();

    /// <inheritdoc/>
    public Task<AppleReportRequest> FindAppleReportRequestAsync(string secretJson, string appExternalId, CancellationToken cancellationToken)
    {
        Fail(appExternalId);

        return Task.FromResult(AppleRequests.GetValueOrDefault(appExternalId));
    }

    /// <inheritdoc/>
    public Task<AppleReportRequest> CreateAppleReportRequestAsync(string secretJson, string appExternalId, CancellationToken cancellationToken)
    {
        Fail(appExternalId);

        if (AppleRequests.TryGetValue(appExternalId, out var existing) && !existing.Stopped)
            return Task.FromResult(existing);

        var request = new AppleReportRequest($"rr-{Interlocked.Increment(ref _sequence)}-{appExternalId}", false);

        AppleRequests[appExternalId] = request;
        CreatedRequests.Enqueue(appExternalId);

        return Task.FromResult(request);
    }

    /// <inheritdoc/>
    public Task<StoreMetricBatch> ReadAppleMetricsAsync(string secretJson, string requestId, DateOnly? processedFrom, CancellationToken cancellationToken)
    {
        var appId = AppleRequests.FirstOrDefault(r => r.Value.Id == requestId).Key;

        Fail(appId);
        Reads.Enqueue((appId, requestId, processedFrom, null));

        return Task.FromResult(AppleBatches.GetValueOrDefault(requestId) ?? new StoreMetricBatch([], null, ReportsReady: false));
    }

    /// <inheritdoc/>
    public Task<StoreMetricBatch> ReadPlayMetricsAsync(string secretJson, string bucket, string packageName, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        Fail(packageName);
        Reads.Enqueue((packageName, bucket, from, to));

        return Task.FromResult(new StoreMetricBatch(PlayRows.GetValueOrDefault(packageName) ?? []));
    }

    /// <inheritdoc/>
    public Task<StoreMetricBatch> ReadMicrosoftMetricsAsync(string secretJson, string productId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        Fail(productId);
        Reads.Enqueue((productId, null, from, to));

        return Task.FromResult(new StoreMetricBatch(MicrosoftRows.GetValueOrDefault(productId) ?? []));
    }

    private void Fail(string appId)
    {
        if (appId != null && Failures.TryGetValue(appId, out var failure))
            throw failure;
    }
}
