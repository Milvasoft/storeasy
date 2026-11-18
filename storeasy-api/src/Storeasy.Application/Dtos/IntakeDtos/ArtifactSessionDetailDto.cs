using Storeasy.Domain.Intake;

namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A package upload session with its steps (tools/CONTRACT.md 3.4, design 13.14).
/// </summary>
public class ArtifactSessionDetailDto : ArtifactSessionListDto
{
    /// <summary>
    /// Processing steps.
    /// </summary>
    public List<ArtifactStep> Steps { get; set; } = [];

    /// <summary>
    /// What was read from the package.
    /// </summary>
    public ArtifactPackageInfo Package { get; set; }

    /// <summary>
    /// Catalog checks.
    /// </summary>
    public List<ArtifactCheck> Checks { get; set; } = [];

    /// <summary>
    /// Delivery target that was applied.
    /// </summary>
    public ArtifactAppliedRule AppliedRule { get; set; }

    /// <summary>
    /// How the upload was authenticated.
    /// </summary>
    public ArtifactIdentityDto Identity { get; set; }

    /// <summary>
    /// Why delivery waits for a member.
    /// </summary>
    public string HoldReason { get; set; }

    /// <summary>
    /// Result for the CI; null until final.
    /// </summary>
    public ArtifactCiResultDto CiResult { get; set; }

    /// <summary>
    /// Whether the stored file was deleted.
    /// </summary>
    public bool PackageDeleted { get; set; }
}
