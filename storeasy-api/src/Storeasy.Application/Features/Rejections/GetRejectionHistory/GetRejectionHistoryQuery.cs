using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Rejections.GetRejectionHistory;

/// <summary>
/// The submission history of an app: every version seen with App Review (accepted ones too) with its rejection cases.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRejectionHistoryQuery : IQuery<RejectionHistoryDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }
}
