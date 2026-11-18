using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Processing state of a build in its store, the same scale for every store.
/// </summary>
public enum BuildProcessingState : byte
{
    /// <summary>
    /// The store is still processing the upload.
    /// </summary>
    [Description("Processing")]
    Processing = 1,

    /// <summary>
    /// Ready to be used (attached to a version, released to a track).
    /// </summary>
    [Description("Valid")]
    Valid = 2,

    /// <summary>
    /// Processing failed.
    /// </summary>
    [Description("Failed")]
    Failed = 3,

    /// <summary>
    /// The store rejected the build.
    /// </summary>
    [Description("Invalid")]
    Invalid = 4,

    /// <summary>
    /// The build expired (TestFlight builds expire after 90 days).
    /// </summary>
    [Description("Expired")]
    Expired = 5,
}
