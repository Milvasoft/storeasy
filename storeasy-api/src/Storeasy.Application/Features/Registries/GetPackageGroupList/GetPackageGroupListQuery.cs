using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetPackageGroupList;

/// <summary>
/// The package groups of the organization: each member's latest version and release date, which members are behind the
/// group's highest version, and open alerts.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPackageGroupListQuery : IQuery<List<PackageGroupDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
