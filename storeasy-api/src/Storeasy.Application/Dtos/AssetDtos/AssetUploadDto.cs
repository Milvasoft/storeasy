namespace Storeasy.Application.Dtos.AssetDtos;

/// <summary>
/// Where to upload a new file. A small file has one <see cref="UploadUrl"/> (PUT the body with the declared
/// <c>Content-Type</c>); a large one has <see cref="Parts"/> (PUT each slice of <see cref="PartSize"/> bytes, keep each
/// response's <c>ETag</c>, then complete with them). Then call the complete endpoint.
/// </summary>
public class AssetUploadDto
{
    /// <summary>
    /// The new asset's id.
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// Presigned PUT URL of a single part upload. Null for a multipart upload.
    /// </summary>
    public string UploadUrl { get; set; }

    /// <summary>
    /// Bytes per part of a multipart upload (the last part may be smaller). Null for a single upload.
    /// </summary>
    public long? PartSize { get; set; }

    /// <summary>
    /// Presigned PUT URLs of the parts, in order. Empty for a single upload.
    /// </summary>
    public List<AssetUploadPartDto> Parts { get; set; } = [];

    /// <summary>
    /// UTC instant the URLs stop working.
    /// </summary>
    public DateTime ExpiryDate { get; set; }
}
