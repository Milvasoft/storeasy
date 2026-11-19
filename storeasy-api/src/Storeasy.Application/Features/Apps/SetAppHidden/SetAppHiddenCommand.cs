using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Apps.SetAppHidden;

/// <summary>
/// Hides an app from the apps list (and the organization wide overviews) or shows it again. Nothing is deleted: the app,
/// its records and builds stay, sync keeps reading them, and the app still opens by its address. Returns the app id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SetAppHiddenCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App to hide or show.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// True hides the app; false shows it again.
    /// </summary>
    public bool Hidden { get; set; }
}
