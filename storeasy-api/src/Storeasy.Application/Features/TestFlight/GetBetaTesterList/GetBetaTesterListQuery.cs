using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.TestFlightDtos;

namespace Storeasy.Application.Features.TestFlight.GetBetaTesterList;

/// <summary>
/// Pages through the TestFlight testers of an App Store record (optionally one group).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetBetaTesterListQuery : ListRequest, IListRequestQuery<BetaTesterDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Only the testers of this group.
    /// </summary>
    public int? GroupId { get; set; }
}
