namespace Storeasy.Domain.JsonModels;

/// <summary>
/// The App Review screenshot of a subscription or In-App Purchase as the store has it.
/// </summary>
public class ProductScreenshot
{
    /// <summary>
    /// Store's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Width in pixels.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height in pixels.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Lower case hex MD5 the store keeps (sourceFileChecksum).
    /// </summary>
    public string Checksum { get; set; }

    /// <summary>
    /// Delivery state (COMPLETE, FAILED...).
    /// </summary>
    public string State { get; set; }
}
