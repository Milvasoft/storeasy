using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Parts of a store's content a read covers.
/// </summary>
[Flags]
public enum ContentParts : short
{
    /// <summary>
    /// Nothing beyond the version.
    /// </summary>
    [Description("None")]
    None = 0,

    /// <summary>
    /// Listing texts.
    /// </summary>
    [Description("Texts")]
    Texts = 1,

    /// <summary>
    /// Categories, copyright, content rights.
    /// </summary>
    [Description("Categories")]
    Categories = 2,

    /// <summary>
    /// Age rating declaration.
    /// </summary>
    [Description("Age rating")]
    AgeRating = 4,

    /// <summary>
    /// Price schedule and territory availability.
    /// </summary>
    [Description("Pricing")]
    Pricing = 8,

    /// <summary>
    /// Screenshot sets and graphics.
    /// </summary>
    [Description("Screenshots")]
    Screenshots = 16,

    /// <summary>
    /// App preview sets.
    /// </summary>
    [Description("Previews")]
    Previews = 32,

    /// <summary>
    /// App Review details.
    /// </summary>
    [Description("Review")]
    Review = 64,

    /// <summary>
    /// Versions, attached build, release type, phased release, tracks.
    /// </summary>
    [Description("Release")]
    Release = 128,

    /// <summary>
    /// Play app details.
    /// </summary>
    [Description("Details")]
    Details = 256,

    /// <summary>
    /// Play tester groups.
    /// </summary>
    [Description("Testers")]
    Testers = 512,

    /// <summary>
    /// Everything.
    /// </summary>
    [Description("All")]
    All = 1023,
}
