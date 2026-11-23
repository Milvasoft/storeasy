using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetTrackedPackageList;

/// <summary>
/// The package summary of the organization: every tracked package with its registry, latest version, publish date, the CI
/// run it came from, provenance and the number of open alerts.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetTrackedPackageListQuery : IQuery<List<TrackedPackageSummaryDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only the packages of this registry.
    /// </summary>
    public PackageRegistry? Registry { get; set; }

    /// <summary>
    /// Only the packages linked to this storeasy app.
    /// </summary>
    public int? AppId { get; set; }
}
