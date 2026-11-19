using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using System.Collections.Concurrent;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The events phase store calls without a store: App Store Connect webhooks and versions, reviews of both stores and
/// TestFlight. Keyed by the store's app id (tests use unique ids). Records every write; never submits anything anywhere.
/// </summary>
public sealed class FakeStoreEventGateway : IStoreEventGateway, IStoreReviewGateway, ITestFlightGateway
{
    private int _sequence;

    /// <summary>Versions by version id (webhook processing).</summary>
    public ConcurrentDictionary<string, StoreVersionRecord> Versions { get; } = new();

    /// <summary>Version lists by app id (version state sync).</summary>
    public ConcurrentDictionary<string, List<StoreVersionRecord>> VersionLists { get; } = new();

    /// <summary>Webhook registrations made: (app id, webhook id, registration).</summary>
    public ConcurrentQueue<(string AppId, string WebhookId, StoreWebhookRegistration Registration)> Webhooks { get; } = new();

    /// <summary>Webhooks deleted.</summary>
    public ConcurrentQueue<string> DeletedWebhooks { get; } = new();

    public ConcurrentQueue<string> Pings { get; } = new();

    /// <summary>Apps whose webhook registration is refused (403).</summary>
    public ConcurrentDictionary<string, bool> RefuseWebhooks { get; } = new();

    /// <summary>Reviews by app id.</summary>
    public ConcurrentDictionary<string, List<StoreReviewRecord>> Reviews { get; } = new();

    /// <summary>Replies published: (store, app id, review id, existing reply id, body).</summary>
    public ConcurrentQueue<(StoreKind Store, string AppId, string ReviewId, string ExistingReplyId, string Body)> Replies { get; } = new();

    /// <summary>Reply failures by review id.</summary>
    public ConcurrentDictionary<string, StoreCallException> ReplyFailures { get; } = new();

    /// <summary>TestFlight state by app id.</summary>
    public ConcurrentDictionary<string, TestFlightSnapshot> TestFlight { get; } = new();

    /// <summary>TestFlight writes: (app id, change).</summary>
    public ConcurrentQueue<(string AppId, TestFlightChange Change)> Changes { get; } = new();

    /// <inheritdoc/>
    public Task<string> UpsertWebhookAsync(string secretJson, string appExternalId, string existingWebhookId, StoreWebhookRegistration registration, CancellationToken cancellationToken)
    {
        if (RefuseWebhooks.ContainsKey(appExternalId))
            throw new StoreCallException("403: The API key is not allowed to manage webhooks.", 403, false);

        var id = existingWebhookId ?? $"wh-{Interlocked.Increment(ref _sequence)}";

        Webhooks.Enqueue((appExternalId, id, registration));

        return Task.FromResult(id);
    }

    /// <inheritdoc/>
    public Task DeleteWebhookAsync(string secretJson, string webhookId, CancellationToken cancellationToken)
    {
        DeletedWebhooks.Enqueue(webhookId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string> PingWebhookAsync(string secretJson, string webhookId, CancellationToken cancellationToken)
    {
        Pings.Enqueue(webhookId);

        return Task.FromResult($"ping-{Interlocked.Increment(ref _sequence)}");
    }

    public Task<StoreVersionRecord> GetVersionAsync(string secretJson, string versionId, CancellationToken cancellationToken)
        => Versions.TryGetValue(versionId, out var version) ? Task.FromResult(version) : throw new StoreCallException("404: no such version", 404, false);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreVersionRecord>> ListVersionsAsync(string secretJson, string appExternalId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StoreVersionRecord>>(VersionLists.TryGetValue(appExternalId, out var list) ? [.. list] : []);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreReviewRecord>> ListReviewsAsync(StoreKind store, string secretJson, string appExternalId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StoreReviewRecord>>(Reviews.TryGetValue(appExternalId, out var list) ? [.. list] : []);

    /// <inheritdoc/>
    public Task<StoreReplyRecord> ReplyAsync(StoreKind store, string secretJson, string appExternalId, string reviewExternalId, string existingReplyExternalId, string body, CancellationToken cancellationToken)
    {
        if (ReplyFailures.TryGetValue(reviewExternalId, out var failure))
            throw failure;

        Replies.Enqueue((store, appExternalId, reviewExternalId, existingReplyExternalId, body));

        return Task.FromResult(new StoreReplyRecord(store == StoreKind.Apple ? $"resp-{reviewExternalId}" : null, DateTime.UtcNow));
    }

    /// <inheritdoc/>
    public Task<TestFlightSnapshot> ReadAsync(string secretJson, string appExternalId, CancellationToken cancellationToken)
        => Task.FromResult(TestFlight.TryGetValue(appExternalId, out var snapshot) ? snapshot : new TestFlightSnapshot([], [], [], []));

    /// <inheritdoc/>
    public Task<string> ApplyAsync(string secretJson, string appExternalId, TestFlightChange change, CancellationToken cancellationToken)
    {
        Changes.Enqueue((appExternalId, change));

        return Task.FromResult($"created-{Interlocked.Increment(ref _sequence)}");
    }
}
