using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Registries.SaveReleaseSettings;

/// <summary>
/// Saves (or clears with null <see cref="Settings"/>) how a release of a package or a group starts in its repository's CI:
/// repository, branch, trigger (version bump, tag, bump and tag, workflow dispatch), version files, tag pattern and the
/// workflow. Nothing changes in the repository.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SaveReleaseSettingsCommand : ICommand<int>, IOrganizationRequest
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
    /// The settings; null clears them.
    /// </summary>
    public ReleaseSettings Settings { get; set; }
}
