namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// The raw screenshot of one slot for one device family (and optionally one locale).
/// </summary>
public class MockupSlotDto
{
    /// <summary>
    /// Slot position, from 1.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Device family of the screenshot.
    /// </summary>
    public MockupDeviceFamily Family { get; set; }

    /// <summary>
    /// Locale the screenshot is for; null for every locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Uploaded screenshot asset.
    /// </summary>
    public long AssetId { get; set; }
}
