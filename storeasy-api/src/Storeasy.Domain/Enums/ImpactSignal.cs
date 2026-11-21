using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How sure the conversion rate change of a before and after report is (two proportion z-test). Values are explicit and
/// never reused.
/// </summary>
public enum ImpactSignal : byte
{
    /// <summary>Too few page views or downloads in a window to tell.</summary>
    [Description("Not enough data")]
    InsufficientData = 0,

    /// <summary>The change is within what chance explains (p at least 0.10).</summary>
    [Description("Not significant")]
    NotSignificant = 1,

    /// <summary>Probably a real change (p below 0.10).</summary>
    [Description("Suggestive")]
    Suggestive = 2,

    /// <summary>A real change with 95 percent confidence (p below 0.05).</summary>
    [Description("Significant")]
    Significant = 3,

    /// <summary>The rate is not a proportion (more downloads than page views), so the test does not apply.</summary>
    [Description("Not applicable")]
    NotApplicable = 4,
}
