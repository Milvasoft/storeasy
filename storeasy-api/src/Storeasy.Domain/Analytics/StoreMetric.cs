using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Analytics;

/// <summary>
/// One day of store page performance of one store record, for one slice: every country and source
/// (<see cref="Country"/> empty, <see cref="Source"/> <see cref="TrafficSource.All"/>), one country, one source, or one
/// country and source where the store reports both together. Slices overlap on purpose: a reader picks exactly one slice
/// per question, so nothing is counted twice even where a store reports countries and sources in separate files (Google
/// Play). A metric the store does not report stays null. Rows are upserted on <c>(StoreAppId, Date, Country, Source)</c>,
/// so reading a day again replaces it.
/// </summary>
[Table(TableNames.StoreMetrics)]
[Index(nameof(StoreAppId), nameof(Date), nameof(Country), nameof(Source), IsUnique = true)]
[Index(nameof(OrganizationId), nameof(Date))]
public class StoreMetric : BaseEntity<long>
{
    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record. Its app is the record's current app, so a record moved to another app takes its history along.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Day of the events (the store's reporting day).
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// ISO 3166 country code, <see cref="StoreMetricRules.UnknownCountry"/> when the store did not name it, empty for
    /// every country.
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [MaxLength(StoreMetricRules.CountryMaxLength)]
    public string Country { get; set; }

    /// <summary>
    /// Traffic source, <see cref="TrafficSource.All"/> for every source.
    /// </summary>
    public TrafficSource Source { get; set; }

    /// <summary>
    /// Times the app was shown in a list. Null when the store does not report it.
    /// </summary>
    public long? Impressions { get; set; }

    /// <summary>
    /// Store page views (visitors on Google Play and Microsoft Store). Null when the store does not report it.
    /// </summary>
    public long? PageViews { get; set; }

    /// <summary>
    /// First time downloads (acquisitions). Null when the store does not report it.
    /// </summary>
    public long? Downloads { get; set; }

    /// <summary>
    /// Redownloads. Null when the store does not report it.
    /// </summary>
    public long? Redownloads { get; set; }

    /// <summary>
    /// UTC instant the row was last written.
    /// </summary>
    public DateTime UpdatedDate { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
