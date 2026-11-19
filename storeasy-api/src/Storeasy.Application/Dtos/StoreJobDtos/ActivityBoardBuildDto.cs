namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// A build the activity page names: still processing at the store, or close to its TestFlight expiry.
/// </summary>
public class ActivityBoardBuildDto
{
    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Version string.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Build number or version code.
    /// </summary>
    public string BuildNumber { get; set; }

    /// <summary>
    /// Processing state.
    /// </summary>
    public BuildProcessingState ProcessingState { get; set; }

    /// <summary>
    /// UTC instant of the upload.
    /// </summary>
    public DateTime? UploadedDate { get; set; }

    /// <summary>
    /// UTC instant the TestFlight build expires.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
}
