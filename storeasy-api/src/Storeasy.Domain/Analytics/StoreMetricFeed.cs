using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Analytics;

/// <summary>
/// The metrics feed of one store record: whether the store sends data, the last read and how far the data goes. The App
/// Store's analytics report request id lives here once the reports are enabled.
/// </summary>
[Table(TableNames.StoreMetricFeeds)]
[Index(nameof(StoreAppId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class StoreMetricFeed : BaseEntity<int>
{
    /// <summary>
    /// Column length of <see cref="LastError"/>.
    /// </summary>
    public const int ErrorMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// State of the feed.
    /// </summary>
    public MetricFeedStatus Status { get; set; }

    /// <summary>
    /// App Store: id of the ONGOING analytics report request. Null elsewhere and before enabling.
    /// </summary>
    [MaxLength(100)]
    public string ExternalRequestId { get; set; }

    /// <summary>
    /// UTC instant the reports were enabled (App Store) or first read.
    /// </summary>
    public DateTime? EnabledDate { get; set; }

    /// <summary>
    /// Who enabled the reports (App Store); null when storeasy found a request made elsewhere.
    /// </summary>
    [MaxLength(100)]
    public string EnabledByUserName { get; set; }

    /// <summary>
    /// UTC instant of the last read, successful or not.
    /// </summary>
    public DateTime? LastFetchedDate { get; set; }

    /// <summary>
    /// UTC instant of the last successful read.
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
    /// App Store: processing date of the newest report instance read, so later reads start near it.
    /// </summary>
    public DateOnly? Marker { get; set; }

    /// <summary>
    /// Redacted reason of the last failed read or of the missing setup. Null after a successful read.
    /// </summary>
    [MaxLength(ErrorMaxLength)]
    public string LastError { get; set; }

    /// <summary>
    /// The last sync job.
    /// </summary>
    public long? LastJobId { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
