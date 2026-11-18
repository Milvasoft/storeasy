using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.TestFlightDtos;

namespace Storeasy.Application.Features.TestFlight.GetBetaGroupList;

/// <summary>
/// The TestFlight beta groups of an App Store record.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetBetaGroupListQuery : IQuery<List<BetaGroupDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
