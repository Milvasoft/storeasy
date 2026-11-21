using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.StoreMetricDtos;

namespace Storeasy.Application.Features.StoreMetrics.GetStoreMetricsSummary;

/// <summary>
/// Store page performance of every app of the organization over a date range, with the change against the previous
/// period, from storeasy's stored daily rows. The range works like <c>GetStoreMetricsQuery</c>.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetStoreMetricsSummaryQuery : IQuery<StoreMetricsSummaryDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only this store; null for every store.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// First day; null for <see cref="Days"/> days before <see cref="To"/>.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Last day; null for yesterday (UTC).
    /// </summary>
    public DateOnly? To { get; set; }

    /// <summary>
    /// Range length in days when <see cref="From"/> is null.
    /// </summary>
    public int? Days { get; set; }
}
