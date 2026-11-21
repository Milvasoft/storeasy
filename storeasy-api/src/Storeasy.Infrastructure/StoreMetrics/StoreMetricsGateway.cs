using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Analytics;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.StoreClients;
using Storeasy.Infrastructure.Stores;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.StoreMetrics;

/// <summary>
/// <see cref="IStoreMetricsGateway"/> over <see cref="StoreApiClient"/> (App Store Connect, Google Cloud Storage) and
/// <see cref="IMicrosoftStoreClient"/> (Partner Center analytics).
/// </summary>
/// <param name="client"></param>
/// <param name="microsoftClient"></param>
public sealed class StoreMetricsGateway(StoreApiClient client, IMicrosoftStoreClient microsoftClient) : IStoreMetricsGateway
{
    /// <summary>
    /// Read only scope of Google Cloud Storage, for the Play statistics bucket.
    /// </summary>
    public const string StorageScope = "https://www.googleapis.com/auth/devstorage.read_only";

    /// <summary>
    /// Google Cloud Storage JSON API base address.
    /// </summary>
    public const string StorageBaseUrl = "https://storage.googleapis.com/storage/v1";

    /// <summary>
    /// Most report instances read per report and run (the newest ones; later runs continue from the marker).
    /// </summary>
    public const int MaxAppleInstances = 120;

    /// <summary>
    /// Largest report file read, compressed or not.
    /// </summary>
    public const long MaxFileBytes = 64L * 1024 * 1024;

    private const int _microsoftPages = 10;

    private readonly StoreApiClient _client = client;
    private readonly IMicrosoftStoreClient _microsoftClient = microsoftClient;

    /// <inheritdoc/>
    public async Task<AppleReportRequest> FindAppleReportRequestAsync(string secretJson, string appExternalId, CancellationToken cancellationToken)
    {
        var (data, _) = await _client.AppleListAsync($"{StoreEventsGateway.AppleBase}/v1/apps/{Uri.EscapeDataString(appExternalId)}/analyticsReportRequests?filter[accessType]=ONGOING&limit=50",
                                                     _client.AppleToken(secretJson),
                                                     cancellationToken,
                                                     maxPages: 2);

        var requests = data.Select(r => new AppleReportRequest(StoreApiClient.Text(r, "id"), StoreApiClient.Flag(StoreApiClient.Child(r, "attributes"), "stoppedDueToInactivity")))
                           .Where(r => r.Id != null)
                           .ToList();

        return requests.FirstOrDefault(r => !r.Stopped) ?? requests.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<AppleReportRequest> CreateAppleReportRequestAsync(string secretJson, string appExternalId, CancellationToken cancellationToken)
    {
        var existing = await FindAppleReportRequestAsync(secretJson, appExternalId, cancellationToken);

        if (existing is { Stopped: false })
            return existing;

        try
        {
            var created = await _client.JsonAsync(HttpMethod.Post,
                                                  $"{StoreEventsGateway.AppleBase}/v1/analyticsReportRequests",
                                                  _client.AppleToken(secretJson),
                                                  new { data = new { type = "analyticsReportRequests", attributes = new { accessType = "ONGOING" }, relationships = new { app = new { data = new { type = "apps", id = appExternalId } } } } },
                                                  cancellationToken);

            return new AppleReportRequest(StoreApiClient.Text(StoreApiClient.Child(created, "data"), "id"), false);
        }
        catch (StoreCallException ex) when (ex.StatusCode == 409)
        {
            // Made meanwhile (another member, another tool): use that one.
            var found = await FindAppleReportRequestAsync(secretJson, appExternalId, cancellationToken);

            if (found != null)
                return found;

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<StoreMetricBatch> ReadAppleMetricsAsync(string secretJson, string requestId, DateOnly? processedFrom, CancellationToken cancellationToken)
    {
        var token = _client.AppleToken(secretJson);
        var (reports, _) = await _client.AppleListAsync($"{StoreEventsGateway.AppleBase}/v1/analyticsReportRequests/{Uri.EscapeDataString(requestId)}/reports?limit=200",
                                                        token,
                                                        cancellationToken);

        var engagement = ReportId(reports, AppleAnalyticsReports.EngagementReportName);
        var downloads = ReportId(reports, AppleAnalyticsReports.DownloadsReportName);

        if (engagement == null && downloads == null)
            return new StoreMetricBatch([], null, ReportsReady: false);

        var (engagementRows, engagementMarker) = await ReadAppleReportAsync(token, engagement, processedFrom, AppleAnalyticsReports.ParseEngagement, cancellationToken);
        var (downloadRows, downloadMarker) = await ReadAppleReportAsync(token, downloads, processedFrom, AppleAnalyticsReports.ParseDownloads, cancellationToken);

        var marker = new[] { engagementMarker, downloadMarker }.Where(m => m.HasValue).DefaultIfEmpty(null).Max();

        return new StoreMetricBatch(StoreMetricRows.Merge([.. engagementRows, .. downloadRows]), marker);
    }

    /// <inheritdoc/>
    public async Task<StoreMetricBatch> ReadPlayMetricsAsync(string secretJson, string bucket, string packageName, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var token = await _client.GoogleTokenAsync(secretJson, cancellationToken, StorageScope);
        var accumulator = new StoreMetricAccumulator();

        foreach (var month in PlayStatsReports.Months(from, to))
        {
            var country = await ReadObjectAsync(token, bucket, PlayStatsReports.CountryFile(packageName, month), cancellationToken);

            if (country != null)
                PlayStatsReports.AddCountryFile(DelimitedText.Decode(country), accumulator);

            var traffic = await ReadObjectAsync(token, bucket, PlayStatsReports.TrafficSourceFile(packageName, month), cancellationToken);

            if (traffic != null)
                PlayStatsReports.AddTrafficSourceFile(DelimitedText.Decode(traffic), accumulator);
        }

        return new StoreMetricBatch(accumulator.ToRows());
    }

    /// <inheritdoc/>
    public async Task<StoreMetricBatch> ReadMicrosoftMetricsAsync(string secretJson, string productId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var accumulator = new StoreMetricAccumulator();
        var id = Uri.EscapeDataString(productId);
        string next = $"analytics/appacquisitions?applicationId={id}&startDate={Day(from)}&endDate={Day(to)}&aggregationLevel=day&groupby=date,market,storeClient&top=10000";

        for (var page = 0; next != null && page < _microsoftPages; page++)
        {
            using var document = JsonDocument.Parse((await _microsoftClient.GetAnalyticsAsync(secretJson, next, cancellationToken))?.ToJsonString() ?? "{}");

            MicrosoftAnalyticsReports.AddAcquisitions(document.RootElement, accumulator);

            next = MicrosoftAnalyticsReports.NextLink(document.RootElement);
        }

        // The funnel has no daily breakdown: one call per day.
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var answer = await _microsoftClient.GetAnalyticsAsync(secretJson, $"analytics/funnel?applicationId={id}&startDate={Day(day)}&endDate={Day(day)}", cancellationToken);

            using var document = JsonDocument.Parse(answer?.ToJsonString() ?? "{}");

            MicrosoftAnalyticsReports.AddFunnelDay(document.RootElement, day, accumulator);
        }

        return new StoreMetricBatch(accumulator.ToRows());
    }

    /// <summary>
    /// Inflates a report segment when it is gzip compressed (Apple serves <c>.gz</c> files); plain text is returned as is.
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string Inflate(byte[] bytes)
    {
        if (bytes.Length < 2 || bytes[0] != 0x1F || bytes[1] != 0x8B)
            return DelimitedText.Decode(bytes);

        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    private async Task<(List<StoreMetricRow> Rows, DateOnly? Marker)> ReadAppleReportAsync(string token,
                                                                                           string reportId,
                                                                                           DateOnly? processedFrom,
                                                                                           Func<string, List<StoreMetricRow>> parse,
                                                                                           CancellationToken cancellationToken)
    {
        if (reportId == null)
            return ([], null);

        var (instances, _) = await _client.AppleListAsync($"{StoreEventsGateway.AppleBase}/v1/analyticsReports/{Uri.EscapeDataString(reportId)}/instances?filter[granularity]=DAILY&limit=200",
                                                          token,
                                                          cancellationToken,
                                                          maxPages: 5);

        var selected = instances.Select(i => (Id: StoreApiClient.Text(i, "id"), Processed: ProcessingDate(i)))
                                .Where(i => i.Id != null && i.Processed.HasValue && (processedFrom == null || i.Processed >= processedFrom))
                                .OrderBy(i => i.Processed)
                                .TakeLast(MaxAppleInstances)
                                .ToList();

        var batches = new List<IReadOnlyList<StoreMetricRow>>();

        foreach (var instance in selected)
        {
            var (segments, _) = await _client.AppleListAsync($"{StoreEventsGateway.AppleBase}/v1/analyticsReportInstances/{Uri.EscapeDataString(instance.Id)}/segments?limit=200",
                                                             token,
                                                             cancellationToken,
                                                             maxPages: 2);

            var rows = new List<StoreMetricRow>();

            foreach (var segment in segments)
            {
                var url = StoreApiClient.Text(StoreApiClient.Child(segment, "attributes"), "url");

                // The segment address is presigned: no bearer token goes to it.
                var bytes = url == null ? null : await _client.BytesOrNotFoundAsync(url, null, MaxFileBytes, cancellationToken);

                if (bytes != null)
                    rows.AddRange(parse(Inflate(bytes)));
            }

            // Segments of one instance are parts of one file: their slices add up.
            batches.Add(SumSegments(rows));
        }

        return (StoreMetricRows.LatestPerDay(batches), selected.Count == 0 ? null : selected[^1].Processed);
    }

    private static List<StoreMetricRow> SumSegments(List<StoreMetricRow> rows)
        => [.. rows.GroupBy(r => (r.Date, r.Country, r.Source))
                   .Select(g => new StoreMetricRow(g.Key.Date, g.Key.Country, g.Key.Source, Sum(g, r => r.Impressions), Sum(g, r => r.PageViews), Sum(g, r => r.Downloads), Sum(g, r => r.Redownloads)))];

    private static long? Sum(IEnumerable<StoreMetricRow> rows, Func<StoreMetricRow, long?> value)
        => rows.Select(value).Where(v => v.HasValue).Aggregate((long?)null, (total, v) => (total ?? 0) + v);

    private static string ReportId(List<JsonElement> reports, string name)
        => reports.Where(r => string.Equals(StoreApiClient.Text(StoreApiClient.Child(r, "attributes"), "name"), name, StringComparison.OrdinalIgnoreCase))
                  .Select(r => StoreApiClient.Text(r, "id"))
                  .FirstOrDefault();

    private static DateOnly? ProcessingDate(JsonElement instance)
        => DateOnly.TryParseExact(StoreApiClient.Text(StoreApiClient.Child(instance, "attributes"), "processingDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private Task<byte[]> ReadObjectAsync(string token, string bucket, string objectName, CancellationToken cancellationToken)
        => _client.BytesOrNotFoundAsync($"{StorageBaseUrl}/b/{Uri.EscapeDataString(bucket)}/o/{Uri.EscapeDataString(objectName)}?alt=media", token, MaxFileBytes, cancellationToken);

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
