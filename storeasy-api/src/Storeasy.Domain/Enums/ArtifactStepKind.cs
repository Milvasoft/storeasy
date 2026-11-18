using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// A step of package processing. Values are explicit and never reused.
/// </summary>
public enum ArtifactStepKind : byte
{
    /// <summary>
    /// Upload to storage.
    /// </summary>
    [Description("Upload")]
    Upload = 1,

    /// <summary>
    /// Reading the package.
    /// </summary>
    [Description("Parse")]
    Parse = 2,

    /// <summary>
    /// Readiness checks.
    /// </summary>
    [Description("Check")]
    Check = 3,

    /// <summary>
    /// Choosing the delivery target.
    /// </summary>
    [Description("Rule")]
    Rule = 4,

    /// <summary>
    /// Delivery to the store.
    /// </summary>
    [Description("Deliver")]
    Deliver = 5,

    /// <summary>
    /// The result for the CI.
    /// </summary>
    [Description("Report")]
    Report = 6,
}
