using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.PreReviewDtos;

namespace Storeasy.Application.Features.PreReviews.GetPreReviewDetail;

/// <summary>
/// One pre-review with its report (verdict, likelihood, findings with guidelines and fixes, runtime checks, usage and cost).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPreReviewDetailQuery : IQuery<PreReviewDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Pre-review id.
    /// </summary>
    public long Id { get; set; }
}
