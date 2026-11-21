using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.StoreMetrics.SetReportBucket;

/// <summary>
/// Sets or clears the Cloud Storage bucket a Google Play connection's statistics reports are exported to (Play Console,
/// Download reports, "Cloud Storage URI", e.g. <c>gs://pubsite_prod_rev_01234567890987654321</c>). The service account
/// must be a Play Console user with "View app information and download bulk reports". Not a secret. Audited; when set, the
/// metrics of the connection's apps are read right away. Answers the stored bucket name.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record SetReportBucketCommand : ICommand<string>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Google Play connection.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Bucket name or <c>gs://</c> URI; empty clears it.
    /// </summary>
    public string Bucket { get; set; }
}
