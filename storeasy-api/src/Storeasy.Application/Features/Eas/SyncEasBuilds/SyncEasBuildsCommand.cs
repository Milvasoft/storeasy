using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.SyncEasBuilds;

/// <summary>
/// Queues a read of the recent EAS builds of one app, or of every linked app. Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SyncEasBuildsCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App; empty for every linked app.
    /// </summary>
    public int? AppId { get; set; }
}
