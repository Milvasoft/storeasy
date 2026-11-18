namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// One Microsoft Store change. <see cref="Kind"/> says which section is read; the others are ignored.
/// </summary>
public class MsChangeInputDto
{
    /// <summary>
    /// What the change writes.
    /// </summary>
    public MsChangeKind Kind { get; set; }

    /// <summary>
    /// Package flight for Packages (flight submission) and Rollout of a flight.
    /// </summary>
    public string FlightId { get; set; }

    /// <summary>
    /// Add-on for AddonContent.
    /// </summary>
    public string AddonStoreId { get; set; }

    /// <summary>
    /// Listings.
    /// </summary>
    public List<MsListingDto> Listings { get; set; } = [];

    /// <summary>
    /// Images.
    /// </summary>
    public MsImageChangeDto Images { get; set; }

    /// <summary>
    /// Packages.
    /// </summary>
    public MsPackageChangeDto Packages { get; set; }

    /// <summary>
    /// Price and availability.
    /// </summary>
    public MsPricingDto Pricing { get; set; }

    /// <summary>
    /// The whole MSI/EXE package set.
    /// </summary>
    public List<MsInstallerPackageInputDto> InstallerPackages { get; set; } = [];

    /// <summary>
    /// Rollout change.
    /// </summary>
    public MsRolloutChangeDto Rollout { get; set; }

    /// <summary>
    /// Flight to create.
    /// </summary>
    public MsFlightInputDto Flight { get; set; }

    /// <summary>
    /// Add-on to create.
    /// </summary>
    public MsAddonInputDto Addon { get; set; }

    /// <summary>
    /// Add-on content.
    /// </summary>
    public MsAddonContentDto AddonContent { get; set; }
}
