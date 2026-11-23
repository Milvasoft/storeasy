using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Whether the latest version of a tracked package came from the repository and workflow its trusted publishing setup
/// expects.
/// </summary>
public enum PublisherCheckStatus : byte
{
    /// <summary>
    /// The package has no trusted publishing setup in storeasy.
    /// </summary>
    [Description("Not set up")]
    NotConfigured = 1,

    /// <summary>
    /// The registry does not say which repository or workflow published a version (NuGet, pub.dev, Maven Central,
    /// Packagist), so storeasy cannot check it.
    /// </summary>
    [Description("Cannot be checked")]
    NotVerifiable = 2,

    /// <summary>
    /// The latest version has no provenance yet: publish once from the new workflow.
    /// </summary>
    [Description("Waiting for a release")]
    NoProvenance = 3,

    /// <summary>
    /// The latest version came from the expected repository and workflow.
    /// </summary>
    [Description("Matches")]
    Match = 4,

    /// <summary>
    /// The latest version came from another repository or workflow.
    /// </summary>
    [Description("Does not match")]
    Mismatch = 5,
}
