using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ChromeDtos;

namespace Storeasy.Application.Features.Chrome.AddChromeItem;

/// <summary>
/// Adds a Chrome Web Store item by its extension id (the API cannot list a publisher's items or create one: the first
/// version is uploaded in the developer dashboard). The item is attached to an existing storeasy app or to a new app, and a
/// sync reads its status.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record AddChromeItemCommand : ICommand<ChromeItemAddedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Chrome Web Store connection.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Extension id (32 letters from a to p).
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// storeasy app to attach the item to; null creates a new app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Name of a new app (the API does not return the item's name); defaults to the extension id.
    /// </summary>
    public string Name { get; set; }
}
