using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Apps.UnlinkStoreApp;

/// <summary>
/// Splits a store record (and its builds) off into a new storeasy app named after the record. A record that is already
/// the app's only one stays where it is. Returns the record's app id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UnlinkStoreAppCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record to split off.
    /// </summary>
    public int StoreAppId { get; set; }
}
