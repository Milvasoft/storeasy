using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetReleaseSettings;

/// <summary>
/// The release settings of a package (its own, or its group's) or of a group, with the current version, the next
/// versions and whether a GitHub token is saved.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetReleaseSettingsQuery : IQuery<ReleaseSettingsDto>, IOrganizationRequest
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
    /// Prerelease suffix for the suggested prerelease version (for example <c>rc.1</c>).
    /// </summary>
    public string Prerelease { get; set; }
}
