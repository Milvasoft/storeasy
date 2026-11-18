using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.TestFlightDtos;

namespace Storeasy.Application.Features.TestFlight.GetBetaBuildList;

/// <summary>
/// The TestFlight builds of an App Store record, newest first, with their 90 day expiry, beta review state and groups.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetBetaBuildListQuery : IQuery<List<BetaBuildDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
