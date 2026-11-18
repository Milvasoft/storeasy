namespace Storeasy.Application.Dtos.AssetDtos;

/// <summary>
/// A presigned download URL of a stored file.
/// </summary>
public class AssetDownloadDto
{
    /// <summary>
    /// Presigned GET URL.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// UTC instant the URL stops working.
    /// </summary>
    public DateTime ExpiryDate { get; set; }
}
