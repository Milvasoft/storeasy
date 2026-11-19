using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Apps.ReorderApps;

/// <summary>
/// Sets the order of the organization's apps list. The apps in <see cref="AppIds"/> come first, in that order; every
/// other app of the organization loses its place and follows them by name (as an app that was never placed). Returns the
/// number of placed apps.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record ReorderAppsCommand : ICommand<int>, IOrganizationRequest
{
    /// <summary>
    /// Most apps one request places.
    /// </summary>
    public const int MaxAppCount = 500;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App ids in the new order, the first one first. Each app once; every id must be an app of the organization.
    /// </summary>
    public List<int> AppIds { get; set; }
}
