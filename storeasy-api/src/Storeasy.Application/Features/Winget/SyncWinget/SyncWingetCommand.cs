using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Winget.SyncWinget;

/// <summary>
/// Queues a sync of one winget package: its versions and manifests from winget-pkgs and the review state of its pull
/// requests. The schedule refreshes open pull requests every 15 minutes on its own.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record SyncWingetCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// winget store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
