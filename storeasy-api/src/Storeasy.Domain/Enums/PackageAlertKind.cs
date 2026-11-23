using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kinds of alerts of a tracked package (package registries module).
/// </summary>
public enum PackageAlertKind : byte
{
    /// <summary>
    /// The latest version is deprecated, or a version was deprecated after storeasy started tracking the package.
    /// </summary>
    [Description("Deprecated")]
    Deprecated = 1,

    /// <summary>
    /// A version was yanked (crates.io, PyPI) or unlisted (NuGet) after storeasy started tracking the package.
    /// </summary>
    [Description("Yanked")]
    Yanked = 2,

    /// <summary>
    /// The latest version was published without provenance (not raised for NuGet, which has none).
    /// </summary>
    [Description("No provenance")]
    NoProvenance = 3,

    /// <summary>
    /// A security advisory (OSV.dev) affects the latest version.
    /// </summary>
    [Description("Security advisory")]
    Advisory = 4,

    /// <summary>
    /// The latest version was published by another repository or workflow than the trusted publishing setup expects.
    /// </summary>
    [Description("Publisher mismatch")]
    PublisherMismatch = 5,

    /// <summary>
    /// A long lived registry token a member recorded (never the token itself) expires within 14 days or has expired.
    /// </summary>
    [Description("Token expiring")]
    TokenExpiring = 6,
}
