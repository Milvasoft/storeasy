using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.DetectReleaseSettings;

/// <summary>
/// Reads the workflows of a GitHub repository (with the saved GitHub token, read only) and suggests release settings for a
/// package or a group: tag pushes, workflow dispatches with a version input, version bump commits.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record DetectReleaseSettingsQuery : IQuery<List<ReleaseSuggestionDto>>, IOrganizationRequest
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
    /// Repository (<c>owner/repo</c>); default the package's source repository.
    /// </summary>
    public string Repository { get; set; }
}
