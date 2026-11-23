using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetPackageAlertList;

/// <summary>
/// The alerts of the organization's tracked packages (deprecated, yanked, no provenance, security advisory): open ones by
/// default, open first and newest first.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPackageAlertListQuery : IQuery<List<PackageAlertDto>>, IOrganizationRequest
{
    /// <summary>
    /// Most alerts returned.
    /// </summary>
    public const int MaxAlerts = 500;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only the alerts of this package.
    /// </summary>
    public int? TrackedPackageId { get; set; }

    /// <summary>
    /// Include resolved alerts.
    /// </summary>
    public bool IncludeResolved { get; set; }
}
