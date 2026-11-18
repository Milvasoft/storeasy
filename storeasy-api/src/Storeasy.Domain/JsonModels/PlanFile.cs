namespace Storeasy.Domain.JsonModels;

/// <summary>
/// The check result of one uploaded file of a media plan.
/// </summary>
public class PlanFile
{
    /// <summary>
    /// Asset id.
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Width in pixels, for images.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height in pixels, for images.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Lower case hex MD5 of the content.
    /// </summary>
    public string Md5 { get; set; }

    /// <summary>
    /// Accepted, Skipped (already in the store) or Rejected.
    /// </summary>
    public string Result { get; set; }

    /// <summary>
    /// Why it was rejected or skipped.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// A class the file fits, when it was rejected for its size.
    /// </summary>
    public string SuggestedClassKey { get; set; }
}
