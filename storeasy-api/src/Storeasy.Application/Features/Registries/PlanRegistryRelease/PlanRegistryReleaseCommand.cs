using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.PlanRegistryRelease;

/// <summary>
/// Plans a release of a package or a group in its repository's CI (always a dry run): the version is checked against the
/// registry and the current version, the version files are read from GitHub and the exact changes, the tag or the workflow
/// dispatch are shown. Approving the plan (with the typed confirmation and the production release permission) starts it.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanRegistryReleaseCommand : ICommand<RegistryPlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Package; give this or <see cref="PackageGroupId"/>.
    /// </summary>
    public int? TrackedPackageId { get; set; }

    /// <summary>
    /// Group.
    /// </summary>
    public int? PackageGroupId { get; set; }

    /// <summary>
    /// The new version.
    /// </summary>
    public string Version { get; set; }
}
