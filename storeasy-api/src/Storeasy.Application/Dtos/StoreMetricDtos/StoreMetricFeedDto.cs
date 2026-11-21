namespace Storeasy.Application.Dtos.StoreMetricDtos;

/// <summary>
/// The metrics feed of one store record of an app: whether data arrives, the last read and what is missing.
/// </summary>
public class StoreMetricFeedDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The record's store connection; null when it has none (nothing can be read).
    /// </summary>
    public int? ConnectionId { get; set; }

    /// <summary>
    /// Name of the connection.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// State of the feed. A record never synced reads <see cref="MetricFeedStatus.NotEnabled"/> on the App Store,
    /// <see cref="MetricFeedStatus.NeedsSetup"/> on Google Play without a bucket and <see cref="MetricFeedStatus.Waiting"/>
    /// otherwise.
    /// </summary>
    public MetricFeedStatus Status { get; set; }

    /// <summary>
    /// App Store: whether the analytics reports can be enabled (a connection and no request yet).
    /// </summary>
    public bool CanEnable { get; set; }

    /// <summary>
    /// Google Play: the reports bucket of the connection.
    /// </summary>
    public string ReportBucket { get; set; }

    /// <summary>
    /// When the reports were enabled.
    /// </summary>
    public DateTime? EnabledDate { get; set; }

    /// <summary>
    /// Who enabled them.
    /// </summary>
    public string EnabledByUserName { get; set; }

    /// <summary>
    /// Last read, successful or not.
    /// </summary>
    public DateTime? LastFetchedDate { get; set; }

    /// <summary>
    /// Last successful read.
    /// </summary>
    public DateTime? LastSucceededDate { get; set; }

    /// <summary>
    /// First day with data.
    /// </summary>
    public DateOnly? EarliestDataDate { get; set; }

    /// <summary>
    /// Last day with data.
    /// </summary>
    public DateOnly? LatestDataDate { get; set; }

    /// <summary>
    /// Why the last read failed or what is missing; null after a successful read.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// The last sync job.
    /// </summary>
    public long? LastJobId { get; set; }
}
