namespace Storeasy.Application.Interfaces;

/// <summary>
/// Reads the store page performance reports of the three stores: the App Store Connect Analytics Reports API, the Google
/// Play statistics exported to the developer's Cloud Storage bucket and the Microsoft Store analytics API. Every read
/// returns daily rows already cut into slices (<see cref="StoreMetricRow"/>). Only
/// <see cref="CreateAppleReportRequestAsync"/> writes to a store. Failures surface as <see cref="StoreCallException"/>;
/// 401 and 403 mean the credentials lack a role, scope or bucket access.
/// </summary>
public interface IStoreMetricsGateway
{
    /// <summary>
    /// The app's ONGOING analytics report request, or null when there is none. A request Apple stopped for inactivity is
    /// returned with <see cref="AppleReportRequest.Stopped"/> set, only when no active one exists.
    /// </summary>
    Task<AppleReportRequest> FindAppleReportRequestAsync(string secretJson, string appExternalId, CancellationToken cancellationToken);

    /// <summary>
    /// Asks App Store Connect for the ONGOING analytics reports of an app (the only write of the metrics feature). When
    /// one already exists, that one is returned.
    /// </summary>
    Task<AppleReportRequest> CreateAppleReportRequestAsync(string secretJson, string appExternalId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the daily instances of the "App Store Discovery and Engagement" and "App Downloads" standard reports of a
    /// request whose processing date is on or after <paramref name="processedFrom"/> (all when null).
    /// </summary>
    Task<StoreMetricBatch> ReadAppleMetricsAsync(string secretJson, string requestId, DateOnly? processedFrom, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the monthly store performance files (by country and by traffic source) of a Play app from the reports
    /// bucket, for every month from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    Task<StoreMetricBatch> ReadPlayMetricsAsync(string secretJson, string bucket, string packageName, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the daily acquisitions (by market and store client) and the page views of the acquisition funnel of a
    /// Microsoft Store product, from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    Task<StoreMetricBatch> ReadMicrosoftMetricsAsync(string secretJson, string productId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// An App Store Connect analytics report request.
/// </summary>
/// <param name="Id">Request id.</param>
/// <param name="Stopped">Apple stopped it because nobody read the reports for a long time; a new request is needed.</param>
public sealed record AppleReportRequest(string Id, bool Stopped);

/// <summary>
/// One day of one slice of a store record's metrics, as read from the store.
/// </summary>
/// <param name="Date">Day.</param>
/// <param name="Country">Country code, empty for every country.</param>
/// <param name="Source">Traffic source, <see cref="TrafficSource.All"/> for every source.</param>
/// <param name="Impressions">Impressions, null when not reported.</param>
/// <param name="PageViews">Page views, null when not reported.</param>
/// <param name="Downloads">First time downloads, null when not reported.</param>
/// <param name="Redownloads">Redownloads, null when not reported.</param>
public sealed record StoreMetricRow(DateOnly Date, string Country, TrafficSource Source, long? Impressions, long? PageViews, long? Downloads, long? Redownloads);

/// <summary>
/// The result of one read.
/// </summary>
/// <param name="Rows">Daily rows.</param>
/// <param name="Marker">App Store: processing date of the newest report instance read; null elsewhere or when none.</param>
/// <param name="ReportsReady">App Store: whether Apple has generated the reports yet (false in the first day or two).</param>
public sealed record StoreMetricBatch(IReadOnlyList<StoreMetricRow> Rows, DateOnly? Marker = null, bool ReportsReady = true);
