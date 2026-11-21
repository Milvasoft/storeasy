using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.StoreMetricDtos;

namespace Storeasy.Application.Features.StoreMetrics.GetStoreMetricFeeds;

/// <summary>
/// The metrics feed of every store record of an app: whether data arrives, when it was last read, how far it goes and
/// what the owner must set up (App Store: enable the analytics reports; Google Play: the reports bucket; any store: a
/// missing role or scope).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetStoreMetricFeedsQuery : IQuery<List<StoreMetricFeedDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }
}
