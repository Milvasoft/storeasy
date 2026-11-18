using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.GetMsFlightList;

/// <summary>
/// Lists the package flights of an MSIX app with the recorded names of their groups. Database only.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMsFlightListQuery : IQuery<List<MsFlightDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
