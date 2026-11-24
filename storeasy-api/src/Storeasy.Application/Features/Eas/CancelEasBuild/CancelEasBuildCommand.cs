using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.CancelEasBuild;

/// <summary>
/// Cancels a queued or running EAS build.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record CancelEasBuildCommand : ICommand<EasBuildDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy build id.
    /// </summary>
    public long Id { get; set; }
}
