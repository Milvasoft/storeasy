using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a Microsoft Store change writes.
/// </summary>
public enum MsChangeKind : byte
{
    /// <summary>
    /// Listing texts per language.
    /// </summary>
    [Description("Listings")]
    Listings = 1,

    /// <summary>
    /// Screenshots and logos of one language (MSIX).
    /// </summary>
    [Description("Images")]
    Images = 2,

    /// <summary>
    /// MSIX packages of an app or flight submission, with the gradual rollout percentage.
    /// </summary>
    [Description("Packages")]
    Packages = 3,

    /// <summary>
    /// Base price, trial and visibility (MSIX) or markets, discoverability and pricing model (MSI/EXE).
    /// </summary>
    [Description("Pricing and availability")]
    Pricing = 4,

    /// <summary>
    /// MSI/EXE installer URLs, architectures and install parameters.
    /// </summary>
    [Description("Installer packages")]
    InstallerPackages = 5,

    /// <summary>
    /// Changes the gradual rollout of a published submission.
    /// </summary>
    [Description("Package rollout")]
    Rollout = 6,

    /// <summary>
    /// Creates a package flight.
    /// </summary>
    [Description("Create package flight")]
    CreateFlight = 7,

    /// <summary>
    /// Creates an add-on.
    /// </summary>
    [Description("Create add-on")]
    CreateAddon = 8,

    /// <summary>
    /// Listing texts, price tier and lifetime of an add-on.
    /// </summary>
    [Description("Add-on content")]
    AddonContent = 9,

    /// <summary>
    /// Deletes the pending draft submission.
    /// </summary>
    [Description("Discard draft submission")]
    DiscardSubmission = 10,
}
