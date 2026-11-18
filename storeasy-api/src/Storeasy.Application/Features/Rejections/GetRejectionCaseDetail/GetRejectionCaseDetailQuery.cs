using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Rejections.GetRejectionCaseDetail;

/// <summary>
/// Reads one rejection case with its text, checklist, reply draft and timeline (any member).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRejectionCaseDetailQuery : IQuery<RejectionCaseDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Case.
    /// </summary>
    public int CaseId { get; set; }
}
