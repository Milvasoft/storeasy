using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Apps.SetAppAlertsMuted;

/// <summary>
/// Mutes an app's alerts or turns them on again. A muted app stays everywhere (apps list, its pages, builds, rejection
/// records) and keeps syncing; only the alerting surfaces leave it out: the live activity board and its attention items,
/// and the notifications, e-mails, web pushes and live events sent to members about it. Returns the app id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SetAppAlertsMutedCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App to mute or unmute.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// True mutes the app's alerts; false turns them on again.
    /// </summary>
    public bool Muted { get; set; }
}
