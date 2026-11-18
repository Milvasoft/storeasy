namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// One of storeasy's parametric device frames with its defaults and the screenshot classes drawn with it.
/// </summary>
public class MockupFrameDto
{
    /// <summary>
    /// Frame kind.
    /// </summary>
    public MockupFrameKind Kind { get; set; }

    /// <summary>
    /// Default corner radius as a fraction of the body width.
    /// </summary>
    public float CornerRadius { get; set; }

    /// <summary>
    /// Default bezel as a fraction of the body width.
    /// </summary>
    public float Bezel { get; set; }

    /// <summary>
    /// Cutouts the frame can draw.
    /// </summary>
    public List<string> Cutouts { get; set; }

    /// <summary>
    /// Classes drawn with the frame.
    /// </summary>
    public List<MockupClassDto> Classes { get; set; }
}

/// <summary>
/// A screenshot class of the rules catalog as the studio exports it.
/// </summary>
public class MockupClassDto
{
    /// <summary>
    /// Class key (rules catalog id).
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// English label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Turkish label.
    /// </summary>
    public string LabelTr { get; set; }

    /// <summary>
    /// Export width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Export height.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Raw screenshot family the class prefers.
    /// </summary>
    public MockupDeviceFamily Family { get; set; }

    /// <summary>
    /// Requirement today when the export provides the default selection.
    /// </summary>
    public MockupRequirement Status { get; set; }

    /// <summary>
    /// Date the class becomes required.
    /// </summary>
    public DateOnly? RequiredFrom { get; set; }

    /// <summary>
    /// Most images per locale.
    /// </summary>
    public int MaxCount { get; set; }
}
