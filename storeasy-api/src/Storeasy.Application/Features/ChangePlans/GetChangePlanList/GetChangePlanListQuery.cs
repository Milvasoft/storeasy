using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Domain.Content;

namespace Storeasy.Application.Features.ChangePlans.GetChangePlanList;

/// <summary>
/// Pages through the organization's change plans, newest first (default: the pending ones, the approval queue).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetChangePlanListQuery : ListRequest, IListRequestQuery<ChangePlanListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only plans in this stored state (Pending, Approved, Applied, Rejected, Discarded); null for all.
    /// </summary>
    public ChangePlanStatus? Status { get; set; } = ChangePlanStatus.Pending;

    /// <summary>
    /// Only plans of this app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Only plans of this store.
    /// </summary>
    public StoreKind? Store { get; set; }
}
