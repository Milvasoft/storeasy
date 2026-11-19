using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What the AI reviewer expects the store review to decide. Values are explicit and never reused.
/// </summary>
public enum PreReviewVerdict : byte
{
    /// <summary>Likely to pass review as it is.</summary>
    [Description("Likely approved")]
    LikelyApproved = 1,

    /// <summary>Could pass, but has problems a reviewer may reject.</summary>
    [Description("At risk")]
    AtRisk = 2,

    /// <summary>Likely to be rejected as it is.</summary>
    [Description("Likely rejected")]
    LikelyRejected = 3,
}
