namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// Preview images of a template in one locale and screenshot class.
/// </summary>
public class MockupPreviewDto
{
    /// <summary>
    /// Screenshot class drawn.
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Locale drawn.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Images in slot order.
    /// </summary>
    public List<MockupPreviewImageDto> Images { get; set; }

    /// <summary>
    /// Server time of the render in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// One preview image.
/// </summary>
public class MockupPreviewImageDto
{
    /// <summary>
    /// Slot position.
    /// </summary>
    public int SlotOrder { get; set; }

    /// <summary>
    /// Pixel width of the preview.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Pixel height of the preview.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// The PNG as a <c>data:image/png;base64,...</c> URL (nothing is stored for a preview). Null when the image failed.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Warnings of the image (and its failure, if any).
    /// </summary>
    public List<MockupWarningDto> Warnings { get; set; }
}
