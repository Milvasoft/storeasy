using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetTrackedPackage;

/// <summary>
/// One tracked package as the last sync read it, with its versions (newest first) and its alerts (open first).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetTrackedPackageQuery : IQuery<TrackedPackageDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracked package.
    /// </summary>
    public int TrackedPackageId { get; set; }
}
