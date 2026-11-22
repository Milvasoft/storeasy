using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.FirefoxDtos;

namespace Storeasy.Application.Features.Firefox.AddFirefoxAddon;

/// <summary>
/// Adds one Firefox add-on by its AMO id, guid or slug (the sync of a connection adds every add-on of the account on its own;
/// this attaches one to an existing storeasy app, or adds one the account co-authors). A sync reads it and fixes the record
/// to the numeric id and the guid.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record AddFirefoxAddonCommand : ICommand<FirefoxAddonAddedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Firefox Add-ons connection.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Numeric AMO id, guid (<c>name@example.com</c> or a braced UUID) or slug.
    /// </summary>
    public string Addon { get; set; }

    /// <summary>
    /// storeasy app to attach the add-on to; null creates a new app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Name of a new app; defaults to the add-on reference until the sync reads the name.
    /// </summary>
    public string Name { get; set; }
}
