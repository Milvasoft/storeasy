using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.IntakeSettings.UpdateIntakeSettings;

/// <summary>
/// Changes how long packages are kept (1 to 365 days, default 30) and the largest package accepted.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record UpdateIntakeSettingsCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Days a package is kept.
    /// </summary>
    public int ArtifactRetentionDays { get; set; } = 30;

    /// <summary>
    /// Largest package accepted, in bytes (at most 4 GiB).
    /// </summary>
    public long MaxArtifactBytes { get; set; } = 4L * 1024 * 1024 * 1024;
}
