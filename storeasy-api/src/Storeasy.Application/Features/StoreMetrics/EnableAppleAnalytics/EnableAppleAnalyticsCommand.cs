using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.StoreMetrics.EnableAppleAnalytics;

/// <summary>
/// Asks App Store Connect to generate the ONGOING analytics reports of one App Store record (an analytics report request,
/// the only store write of the metrics feature). It is never done automatically: <see cref="Confirm"/> must be true. The
/// API key needs the Admin role for this step; reading the reports later needs Admin or Sales and Reports. Apple delivers
/// the first data one or two days later. Audited; returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record EnableAppleAnalyticsCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The member confirmed the request to Apple.
    /// </summary>
    public bool Confirm { get; set; }
}
