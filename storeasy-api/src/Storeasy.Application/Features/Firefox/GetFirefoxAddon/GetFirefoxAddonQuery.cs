using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.FirefoxDtos;

namespace Storeasy.Application.Features.Firefox.GetFirefoxAddon;

/// <summary>
/// Reads a Firefox add-on: what the last sync saved (status, versions, rating, users, history) and a live read of the
/// listing, previews and versions, with the Firefox packages intake received for the app.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetFirefoxAddonQuery : IQuery<FirefoxAddonDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Firefox store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
