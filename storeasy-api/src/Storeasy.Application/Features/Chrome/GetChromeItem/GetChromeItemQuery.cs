using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ChromeDtos;

namespace Storeasy.Application.Features.Chrome.GetChromeItem;

/// <summary>
/// A Chrome Web Store item as the last sync read it, with the extension packages intake received for the app.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetChromeItemQuery : IQuery<ChromeItemDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Chrome store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
