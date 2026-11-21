using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a change event of the before and after impact report is. Values are explicit and never reused.
/// </summary>
public enum ImpactEventKind : byte
{
    /// <summary>Store listing texts (name, subtitle, description, keywords, promotional text...).</summary>
    [Description("Listing texts")]
    Listing = 1,

    /// <summary>Screenshots or graphics.</summary>
    [Description("Screenshots")]
    Screenshots = 2,

    /// <summary>App icon (a Google Play graphic).</summary>
    [Description("Icon")]
    Icon = 3,

    /// <summary>App preview videos.</summary>
    [Description("App previews")]
    Previews = 4,

    /// <summary>Base price or availability.</summary>
    [Description("Price")]
    Price = 5,

    /// <summary>Categories.</summary>
    [Description("Category")]
    Category = 6,

    /// <summary>A version went live (App Store ready for sale, a Google Play production release, a published Microsoft Store submission).</summary>
    [Description("Version released")]
    VersionReleased = 10,

    /// <summary>Another Microsoft Store change.</summary>
    [Description("Microsoft Store change")]
    MsChange = 11,

    /// <summary>A store listing experiment started (App Store product page optimization, Google Play store listing experiment).</summary>
    [Description("Experiment started")]
    ExperimentStarted = 20,

    /// <summary>A store listing experiment ended.</summary>
    [Description("Experiment ended")]
    ExperimentEnded = 21,

    /// <summary>The winning treatment of an experiment was applied to the store page.</summary>
    [Description("Experiment winner applied")]
    ExperimentWinnerApplied = 22,
}
