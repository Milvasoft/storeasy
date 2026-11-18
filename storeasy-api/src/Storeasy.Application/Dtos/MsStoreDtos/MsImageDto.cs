namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A listing image of one language (MSIX).
/// </summary>
public class MsImageDto
{
    /// <summary>
    /// Language.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Store's image id; null for a new image.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// File name in the submission.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Image type (Screenshot, MobileScreenshot, XboxScreenshot, SurfaceHubScreenshot, HoloLensScreenshot, StoreLogo9x16, StoreLogoSquare, Icon, PromotionalArt16x9, PromotionalArtwork2400X1200...).
    /// </summary>
    public string ImageType { get; set; }

    /// <summary>
    /// Caption.
    /// </summary>
    public string Description { get; set; }
}
