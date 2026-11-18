namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// Outcome of a caption save: the cells written and the captions that overflow in the narrowest exported class.
/// </summary>
public class MockupCaptionsSavedDto
{
    /// <summary>
    /// Cells written (an empty cell removes its row).
    /// </summary>
    public int Saved { get; set; }

    /// <summary>
    /// Narrowest class the captions were measured in.
    /// </summary>
    public string MeasuredClassKey { get; set; }

    /// <summary>
    /// Overflowing captions and font warnings.
    /// </summary>
    public List<MockupWarningDto> Warnings { get; set; }
}
