using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Chrome.SyncChrome;

/// <summary>
/// Queues a sync of one Chrome Web Store item: its status, rating summary and weekly users. The schedule reads items in
/// review every 15 minutes on its own.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record SyncChromeCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Chrome store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
