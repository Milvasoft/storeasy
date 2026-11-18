using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Readiness.GetAppsOverview;

/// <summary>
/// Every app of the organization in one call (the MCP summary): each store record with its last readiness check (missing counts and keys) and the app's next open deadline. With Refresh every record is checked live first (slow; one store read per record).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAppsOverviewQuery : IQuery<List<AppOverviewDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Check every record live before answering.
    /// </summary>
    public bool Refresh { get; set; }
}
