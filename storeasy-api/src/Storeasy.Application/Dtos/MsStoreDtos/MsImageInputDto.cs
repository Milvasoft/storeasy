namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A new listing image taken from an uploaded asset.
/// </summary>
public class MsImageInputDto
{
    /// <summary>
    /// Uploaded asset (PNG).
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// Image type (Screenshot for desktop screenshots).
    /// </summary>
    public string ImageType { get; set; }

    /// <summary>
    /// Caption, up to 200 characters.
    /// </summary>
    public string Description { get; set; }
}
