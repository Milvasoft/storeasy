namespace Storeasy.Application.Dtos.AppDtos;

/// <summary>
/// A build of one of the app's store records.
/// </summary>
public class BuildDto
{
    /// <summary>
    /// Build id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the build.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// User facing version.
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
    /// Tracks the build is released to (Play).
    /// </summary>
    public string Tracks { get; set; }

    /// <summary>
    /// Minimum OS version.
    /// </summary>
    public string MinOsVersion { get; set; }

    /// <summary>
    /// UTC upload instant.
    /// </summary>
    public DateTime? UploadedDate { get; set; }

    /// <summary>
    /// UTC expiry instant (TestFlight).
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
}
