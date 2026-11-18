using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a package upload session. Values are explicit and never reused.
/// </summary>
public enum ArtifactSessionStatus : byte
{
    /// <summary>
    /// Parts are being uploaded.
    /// </summary>
    [Description("Uploading")]
    Uploading = 1,

    /// <summary>
    /// The package is being read.
    /// </summary>
    [Description("Parsing")]
    Parsing = 2,

    /// <summary>
    /// The package is checked against the rules catalog.
    /// </summary>
    [Description("Checking")]
    Checking = 3,

    /// <summary>
    /// The package is being delivered to the store.
    /// </summary>
    [Description("Delivering")]
    Delivering = 4,

    /// <summary>
    /// The store has the package on the rule's target.
    /// </summary>
    [Description("Delivered")]
    Delivered = 5,

    /// <summary>
    /// The package was refused (wrong app, failed check, size mismatch).
    /// </summary>
    [Description("Rejected")]
    Rejected = 6,

    /// <summary>
    /// The same file was already received for this app.
    /// </summary>
    [Description("Duplicate")]
    Duplicate = 7,

    /// <summary>
    /// Delivery waits for a member (a warning held it, or no rule applies).
    /// </summary>
    [Description("Held")]
    Held = 8,

    /// <summary>
    /// The store refused the delivery or it could not complete.
    /// </summary>
    [Description("Failed")]
    Failed = 9,
}
