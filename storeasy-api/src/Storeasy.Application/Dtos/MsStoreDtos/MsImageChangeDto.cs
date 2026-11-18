namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Images of one language to add and remove (MSIX).
/// </summary>
public class MsImageChangeDto
{
    /// <summary>
    /// Language.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Images to add.
    /// </summary>
    public List<MsImageInputDto> Add { get; set; } = [];

    /// <summary>
    /// File names of the images to remove.
    /// </summary>
    public List<string> RemoveFileNames { get; set; } = [];
}
