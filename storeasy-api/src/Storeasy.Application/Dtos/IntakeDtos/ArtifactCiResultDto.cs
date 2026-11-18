namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// Result for the CI once the session is final.
/// </summary>
public class ArtifactCiResultDto
{
    /// <summary>
    /// Exit code (0 delivered, duplicate or held; 2 rejected; 3 identity; 4 store error).
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// One line summary.
    /// </summary>
    public string Summary { get; set; }
}
