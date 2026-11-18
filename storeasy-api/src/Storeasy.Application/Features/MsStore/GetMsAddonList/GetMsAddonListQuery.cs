using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.GetMsAddonList;

/// <summary>
/// Lists the add-ons of an MSIX app; the ones the API cannot write are marked (assisted step). Database only.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMsAddonListQuery : IQuery<List<MsAddonDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
