using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.PreReviewDtos;

namespace Storeasy.Application.Features.PreReviews.GetPreReviewList;

/// <summary>
/// The pre-review history of one app in one store, newest first (at most <see cref="MaxRows"/>).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPreReviewListQuery : IQuery<List<PreReviewListDto>>, IOrganizationRequest, IAppStoreRequest
{
    /// <summary>
    /// Most pre-reviews returned.
    /// </summary>
    public const int MaxRows = 30;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }
}
