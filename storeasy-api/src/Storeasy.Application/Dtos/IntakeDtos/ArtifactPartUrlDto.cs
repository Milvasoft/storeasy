namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A presigned PUT URL of one part.
/// </summary>
public class ArtifactPartUrlDto
{
    /// <summary>
    /// Part number, from 1.
    /// </summary>
    public int PartNumber { get; set; }

    /// <summary>
    /// Presigned PUT URL.
    /// </summary>
    public string Url { get; set; }
}
