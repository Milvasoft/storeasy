using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.StoreDtos;

namespace Storeasy.Application.Features.StoreAuditLogs.GetStoreAuditLogList;

/// <summary>
/// Pages through an organization's audit log, newest first. The standard list request filters (date range, action,
/// user name, channel, store) apply to the DTO fields.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetStoreAuditLogListQuery : ListRequest, IListRequestQuery<StoreAuditLogDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
