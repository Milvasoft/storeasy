using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetRegistryPlanList;

/// <summary>
/// The release and operation plans of the organization, newest first (of one package or group when given).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRegistryPlanListQuery : IQuery<List<RegistryPlanDto>>, IOrganizationRequest
{
    /// <summary>
    /// Most plans returned.
    /// </summary>
    public const int MaxPlans = 100;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only the plans of this package.
    /// </summary>
    public int? TrackedPackageId { get; set; }

    /// <summary>
    /// Only the plans of this group.
    /// </summary>
    public int? PackageGroupId { get; set; }
}
