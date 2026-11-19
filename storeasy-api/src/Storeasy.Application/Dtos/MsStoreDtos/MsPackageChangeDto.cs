namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Packages of an MSIX app or flight submission to add and remove, and the gradual rollout of the new ones.
/// </summary>
public class MsPackageChangeDto
{
    /// <summary>
    /// Uploaded packages (.msix, .msixbundle, .msixupload, .appx, .appxbundle, .appxupload).
    /// </summary>
    public List<long> AddAssetIds { get; set; } = [];

    /// <summary>
    /// File names of the packages to remove.
    /// </summary>
    public List<string> RemoveFileNames { get; set; } = [];

    /// <summary>
    /// Gradual rollout percentage of the new packages (app submissions); null for all customers at once.
    /// </summary>
    public double? RolloutPercentage { get; set; }

    /// <summary>
    /// Release notes ("What's new") written into every listing language of the app submission; null keeps the copied
    /// notes. Flight submissions have no listings, so it is not used there.
    /// </summary>
    public string ReleaseNotes { get; set; }
}
