using Milvasoft.Attributes.Annotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Current Microsoft Store content of a product, flight or add-on as read from the store.
/// </summary>
public class MsContentDto
{
    /// <summary>
    /// Which API manages the product.
    /// </summary>
    public MsProductKind Kind { get; set; }

    /// <summary>
    /// Submission target.
    /// </summary>
    public MsSubmissionTarget Target { get; set; }

    /// <summary>
    /// Store id of the app or product.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Submission the content comes from: the pending one, else the last published one.
    /// </summary>
    public string SubmissionId { get; set; }

    /// <summary>
    /// Whether that submission is pending.
    /// </summary>
    public bool SubmissionIsPending { get; set; }

    /// <summary>
    /// Status of that submission.
    /// </summary>
    public MsSubmissionStatus? SubmissionStatus { get; set; }

    /// <summary>
    /// Listings.
    /// </summary>
    public List<MsListingDto> Listings { get; set; } = [];

    /// <summary>
    /// Images.
    /// </summary>
    public List<MsImageDto> Images { get; set; } = [];

    /// <summary>
    /// Packages.
    /// </summary>
    public List<MsPackageDto> Packages { get; set; } = [];

    /// <summary>
    /// Price and availability.
    /// </summary>
    public MsPricingDto Pricing { get; set; }

    /// <summary>
    /// Gradual rollout of the submission.
    /// </summary>
    public MsRolloutDto Rollout { get; set; }

    /// <summary>
    /// MSI/EXE installer packages.
    /// </summary>
    public List<MsInstallerPackageInputDto> InstallerPackages { get; set; } = [];

    /// <summary>
    /// Add-on listings.
    /// </summary>
    public List<MsAddonListingDto> AddonListings { get; set; } = [];

    /// <summary>
    /// Add-on lifetime.
    /// </summary>
    public string AddonLifetime { get; set; }

    /// <summary>
    /// Whether the price is on Pricing Version 2 (Partner Center only).
    /// </summary>
    public bool PricingV2 { get; set; }

    /// <summary>
    /// Whether the submission makes its packages a mandatory update.
    /// </summary>
    public bool MandatoryUpdate { get; set; }

    /// <summary>
    /// Fingerprint of the submission content (changes when anyone edits it).
    /// </summary>
    public string ContentHash { get; set; }

    /// <summary>
    /// The submission as the API returned it (MSIX), or the draft metadata (MSI/EXE). Never sent to clients.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public JsonObject Document { get; set; }
}
