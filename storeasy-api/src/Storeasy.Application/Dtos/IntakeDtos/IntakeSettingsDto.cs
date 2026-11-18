namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// Package intake settings of an organization.
/// </summary>
public class IntakeSettingsDto
{
    /// <summary>
    /// Days a package is kept.
    /// </summary>
    public int ArtifactRetentionDays { get; set; }

    /// <summary>
    /// Largest package accepted.
    /// </summary>
    public long MaxArtifactBytes { get; set; }
}
