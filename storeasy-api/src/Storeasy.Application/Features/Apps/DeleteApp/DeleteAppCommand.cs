using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Apps.DeleteApp;

/// <summary>
/// Deletes an app whose store records were all added by id (Chrome Web Store items, Firefox add-ons, winget packages), with
/// those records, their builds and every row that hangs on them. An app with an App Store, Google Play or Microsoft Store
/// record is refused: sync would read the record again, so hide it instead. Returns the deleted app id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record DeleteAppCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App to delete.
    /// </summary>
    public int AppId { get; set; }
}
