using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Readiness.GetAppCompliance;

/// <summary>
/// The compliance timeline of one app: every catalog deadline that applies (or may apply) to it in every store, with its state (done, missing, unknown).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAppComplianceQuery : IQuery<List<ComplianceItemDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }
}
