using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Rejections.GetRejectionCaseList;

/// <summary>
/// Pages through the organization's rejection cases, newest first (any member).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRejectionCaseListQuery : ListRequest, IListRequestQuery<RejectionCaseListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only the cases of this app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Only open or only closed cases.
    /// </summary>
    public RejectionState? State { get; set; }
}
