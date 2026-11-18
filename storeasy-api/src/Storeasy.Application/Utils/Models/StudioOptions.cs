namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Mockup studio settings (configuration section <see cref="SectionName"/>), bound once and registered as a singleton.
/// </summary>
public class StudioOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Studio";

    /// <summary>
    /// Folder of the embedded fonts. Empty: <c>StudioFonts</c> next to the application.
    /// </summary>
    public string FontsPath { get; set; }

    /// <summary>
    /// Widest preview image in pixels.
    /// </summary>
    public int PreviewMaxWidth { get; set; } = 800;

    /// <summary>
    /// Memory the raw screenshot cache may use, in megabytes.
    /// </summary>
    public int AssetCacheMegabytes { get; set; } = 128;
}
