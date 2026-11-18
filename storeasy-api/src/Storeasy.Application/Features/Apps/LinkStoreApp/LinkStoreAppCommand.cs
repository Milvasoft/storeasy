using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Apps.LinkStoreApp;

/// <summary>
/// Moves a store record (and its builds) to another storeasy app, e.g. to pair an iOS and an Android record of one
/// product. The target may hold at most one record per store; an app left without records is removed. Returns the
/// target app id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record LinkStoreAppCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Target storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record to move.
    /// </summary>
    public int StoreAppId { get; set; }
}
