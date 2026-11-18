namespace Storeasy.Application.Dtos.AssetDtos;

/// <summary>
/// One part of a multipart upload: its number and its presigned PUT URL.
/// </summary>
public class AssetUploadPartDto
{
    /// <summary>
    /// Part number, from 1.
    /// </summary>
    public int PartNumber { get; set; }

    /// <summary>
    /// Presigned PUT URL of the part.
    /// </summary>
    public string Url { get; set; }
}
