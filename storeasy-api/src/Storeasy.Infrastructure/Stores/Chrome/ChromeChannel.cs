using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Analytics;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// The Chrome Web Store engine as a store channel: the credential is the publisher id with a service account key or an
/// OAuth client, a product is an extension id, its builds are the versions of its published and submitted revisions. The
/// API cannot list a publisher's items, so items are added by id. It also reads the reviews (no replies: the API has none)
/// and the store metrics (installs, impressions, page views by country).
/// </summary>
/// <param name="client"></param>
/// <param name="options"></param>
public sealed class ChromeChannel(IChromeWebStoreClient client, ChromeOptions options) : IStoreChannel, IStoreChannelReviews, IStoreChannelMetrics
{
    /// <summary>
    /// Metrics read into the store metrics table.
    /// </summary>
    public static readonly IReadOnlyList<string> Metrics = ["installs", "impressions", "page-views"];

    private readonly IChromeWebStoreClient _client = client;
    private readonly ChromeOptions _options = options;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Chrome;

    /// <inheritdoc/>
    public int BackfillDays => _options.MetricsBackfillDays;

    /// <inheritdoc/>
    public StoreSecretInspection Inspect(string secretJson) => InspectSecret(secretJson);

    /// <summary>
    /// The offline check of a Chrome secret (also used without a client, by the tests' gateway).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <returns></returns>
    public static StoreSecretInspection InspectSecret(string secretJson)
    {
        var credentials = ChromeCredentialParser.Parse(secretJson, out var errorKey);

        if (credentials == null)
            return new StoreSecretInspection(false, errorKey, null, null);

        var account = credentials.ServiceAccount;
        var fingerprint = new ConnectionFingerprint
        {
            PublisherId = credentials.PublisherId,
            ServiceAccountEmail = account?.ClientEmail,
            ProjectId = account?.ProjectId,
            ClientIdSuffix = credentials.IsServiceAccount ? null : ChromeCredentialParser.ClientIdSuffix(credentials.ClientId),
            TokenKind = credentials.IsServiceAccount ? "service account" : "OAuth client",
        };

        return new StoreSecretInspection(true, null, fingerprint, JsonSerializer.Serialize(credentials));
    }

    /// <inheritdoc/>
    public Task<StoreVerification> VerifyAsync(string secretJson, string productId, CancellationToken cancellationToken) => _client.VerifyAsync(secretJson, productId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreAppRecord>> ListAppsAsync(string secretJson, CancellationToken cancellationToken)
        => throw new StoreCallException("The Chrome Web Store API cannot list a publisher's items; add the item by its extension id.", null, false);

    /// <inheritdoc/>
    public async Task<StoreAppRecord> GetAppAsync(string secretJson, string externalId, CancellationToken cancellationToken)
    {
        // The API has no read of the store listing, so the item has no name here; the id stands in for it.
        var status = await _client.FetchStatusAsync(secretJson, externalId, cancellationToken);

        return new StoreAppRecord(status.ItemId ?? externalId, externalId, externalId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreBuildRecord>> ListBuildsAsync(string secretJson, string externalId, CancellationToken cancellationToken)
        => BuildsOf(await _client.FetchStatusAsync(secretJson, externalId, cancellationToken));

    /// <summary>
    /// The versions of the published and submitted revisions as builds.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static List<StoreBuildRecord> BuildsOf(ChromeItemStatusRead status)
        => [.. new[] { status.Published, status.Submitted }.Where(r => r?.Version != null)
                                                           .DistinctBy(r => r.Version)
                                                           .Select(r => new StoreBuildRecord(r.Version, r.Version, r.Version, r.State == ChromeItemState.Rejected ? BuildProcessingState.Invalid : BuildProcessingState.Valid, null, null, r.State.ToString(), null))];

    /// <inheritdoc/>
    public Task<string> GetIconUrlAsync(string secretJson, string externalId, CancellationToken cancellationToken) => Task.FromResult<string>(null);

    /// <inheritdoc/>
    public StoreJobRequest SyncJob(int organizationId, int connectionId) => new(organizationId, StoreJobKind.ChromeSync, new ChromeSyncPayload(connectionId), StoreKind.Chrome);

    /// <inheritdoc/>
    public bool SameAccount(ConnectionFingerprint current, ConnectionFingerprint next)
        => current?.PublisherId == null || string.Equals(current.PublisherId, next?.PublisherId, StringComparison.Ordinal);

    /// <inheritdoc/>
    public string CredentialLabel(ConnectionFingerprint fingerprint)
        => fingerprint?.PublisherId == null ? null : $"{fingerprint.TokenKind ?? "credential"} {fingerprint.ServiceAccountEmail ?? fingerprint.ClientIdSuffix} for publisher {fingerprint.PublisherId}".Replace("  ", " ", StringComparison.Ordinal);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreReviewRecord>> ListReviewsAsync(string secretJson, string externalId, CancellationToken cancellationToken)
        => _client.FetchReviewsAsync(secretJson, externalId, _options.ReviewPages, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// <c>queryMetrics</c> takes one dimension per request and not every metric supports every dimension (impressions
    /// only take the UTM dimensions), so each metric is read on its own: by country, or for every country when the store
    /// refuses the country dimension for it.
    /// </remarks>
    public async Task<StoreMetricBatch> ReadMetricsAsync(string secretJson, string externalId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = new List<ChromeMetricRead>();

        foreach (var metric in Metrics)
        {
            try
            {
                rows.AddRange(await _client.QueryMetricsAsync(secretJson, externalId, [metric], "country", from, to, cancellationToken));
            }
            catch (StoreCallException ex) when (IsUnsupportedDimension(ex))
            {
                rows.AddRange(await _client.QueryMetricsAsync(secretJson, externalId, [metric], null, from, to, cancellationToken));
            }
        }

        return new StoreMetricBatch(ToMetricRows(rows));
    }

    /// <summary>
    /// True for the store's refusal of a dimension the metric does not support (400, "Invalid format for 'dimensions'").
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static bool IsUnsupportedDimension(StoreCallException exception)
        => exception is { Transient: false, StatusCode: 400 } && exception.Message.Contains("dimensions", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Store metric rows (by country and for every country) of <c>queryMetrics</c> rows sliced by country: installs are
    /// downloads, impressions and page views as they are.
    /// </summary>
    /// <param name="rows"></param>
    /// <returns></returns>
    public static List<StoreMetricRow> ToMetricRows(IEnumerable<ChromeMetricRead> rows)
    {
        var accumulator = new StoreMetricAccumulator();

        foreach (var row in rows)
        {
            var country = string.IsNullOrWhiteSpace(row.Dimension) || row.Dimension.Length != 2 ? null : row.Dimension;

            foreach (var (metric, kind) in new[] { ("installs", StoreMetricKind.Downloads), ("impressions", StoreMetricKind.Impressions), ("page-views", StoreMetricKind.PageViews) })
            {
                if (row.Values.TryGetValue(metric, out var value))
                    accumulator.Add(row.Date, country, TrafficSource.All, kind, value);
            }
        }

        return accumulator.ToRows();
    }
}
