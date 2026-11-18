namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A part already in storage, or a part a client completes with.
/// </summary>
public class ArtifactUploadedPartDto
{
    /// <summary>
    /// Part number, from 1.
    /// </summary>
    public int PartNumber { get; set; }

    /// <summary>
    /// ETag the storage returned for the part.
    /// </summary>
    public string ETag { get; set; }
}
