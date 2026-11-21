using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.StoreMetricDtos;

namespace Storeasy.Application.Features.StoreMetrics.GetStoreMetrics;

/// <summary>
/// Store page performance of one app (impressions, page views, downloads, conversion rate) over a date range, from
/// storeasy's stored daily rows; nothing is read from a store. The range is <see cref="From"/> to <see cref="To"/>, or the
/// last <see cref="Days"/> days ending yesterday (30 by default), at most 400 days.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetStoreMetricsQuery : IQuery<StoreMetricsReportDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Only this store; null for every store of the app.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Only this country (ISO 3166 code, e.g. <c>TR</c>); null for every country.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Only this traffic source; null for every source.
    /// </summary>
    public TrafficSource? Source { get; set; }

    /// <summary>
    /// First day; null for <see cref="Days"/> days before <see cref="To"/>.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Last day; null for yesterday (UTC).
    /// </summary>
    public DateOnly? To { get; set; }

    /// <summary>
    /// Range length in days when <see cref="From"/> is null (7, 30, 90...).
    /// </summary>
    public int? Days { get; set; }
}
