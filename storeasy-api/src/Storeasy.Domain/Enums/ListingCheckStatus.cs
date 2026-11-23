using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Result of one readiness check of a package's registry listing.
/// </summary>
public enum ListingCheckStatus : byte
{
    /// <summary>
    /// The listing has it.
    /// </summary>
    [Description("Ready")]
    Ok = 1,

    /// <summary>
    /// The listing has it, but it could be better (a very short description).
    /// </summary>
    [Description("Could be better")]
    Warning = 2,

    /// <summary>
    /// The listing lacks it.
    /// </summary>
    [Description("Missing")]
    Missing = 3,
}
