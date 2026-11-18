using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.ArtifactSources.SaveArtifactSource;

/// <summary>
/// Creates or updates a pull source (design 13.19, 13.20). The signing secret is returned only when it is created or
/// rotated; storeasy keeps it sealed with the master key because it must check signatures with it.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record SaveArtifactSourceCommand : ICommand<ArtifactSourceSavedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Source to update; null creates one.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// EAS or generic webhook (only on creation).
    /// </summary>
    public ArtifactPullSourceKind Kind { get; set; }

    /// <summary>
    /// App (only on creation).
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Allowed download domains (host or *.host, https only).
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// Platforms whose builds are taken.
    /// </summary>
    public List<ArtifactPlatform> Platforms { get; set; } = [];

    /// <summary>
    /// Whether APK builds are taken as well.
    /// </summary>
    public bool IncludeApk { get; set; }

    /// <summary>
    /// Whether to issue a new secret (the old one stops working).
    /// </summary>
    public bool RotateSecret { get; set; }
}
