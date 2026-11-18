using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a change plan changes in a store.
/// </summary>
public enum ChangePlanKind : byte
{
    /// <summary>
    /// Store listing texts (app info and version localizations, Play listings).
    /// </summary>
    [Description("Store listing texts")]
    Listing = 1,

    /// <summary>
    /// Play app details (contact e-mail, phone, website, default language).
    /// </summary>
    [Description("App details")]
    Details = 2,

    /// <summary>
    /// Categories, copyright and content rights declaration.
    /// </summary>
    [Description("Categories and rights")]
    Categories = 3,

    /// <summary>
    /// Age rating declaration.
    /// </summary>
    [Description("Age rating")]
    AgeRating = 4,

    /// <summary>
    /// Base price and territory availability.
    /// </summary>
    [Description("Pricing and availability")]
    Pricing = 5,

    /// <summary>
    /// Screenshot sets (Apple) and graphics (Play).
    /// </summary>
    [Description("Screenshots and graphics")]
    Screenshots = 6,

    /// <summary>
    /// App preview videos.
    /// </summary>
    [Description("App previews")]
    Previews = 7,

    /// <summary>
    /// App Review contact, demo account and notes.
    /// </summary>
    [Description("Review information")]
    ReviewInfo = 8,

    /// <summary>
    /// Attaching a build to a version.
    /// </summary>
    [Description("Build link")]
    BuildLink = 9,

    /// <summary>
    /// Export compliance answer of a build.
    /// </summary>
    [Description("Export compliance")]
    ExportCompliance = 10,

    /// <summary>
    /// Release type and phased release of a version.
    /// </summary>
    [Description("Release settings")]
    ReleaseSettings = 11,

    /// <summary>
    /// A release on a Play track.
    /// </summary>
    [Description("Track release")]
    TrackRelease = 12,

    /// <summary>
    /// Staged or phased rollout change.
    /// </summary>
    [Description("Rollout")]
    Rollout = 13,

    /// <summary>
    /// Tester groups of a Play track.
    /// </summary>
    [Description("Testers")]
    Testers = 14,

    /// <summary>
    /// Android App Bundle upload.
    /// </summary>
    [Description("Bundle upload")]
    BundleUpload = 15,

    /// <summary>
    /// Play Data safety form.
    /// </summary>
    [Description("Data safety")]
    DataSafety = 16,

    /// <summary>
    /// Submitting a version for App Review.
    /// </summary>
    [Description("Submit for review")]
    SubmitForReview = 17,

    /// <summary>
    /// A Microsoft Store change (listings, images, packages, pricing, installers, rollout, flights, add-ons, discarding a
    /// draft); what it writes is in the plan's Microsoft change set.
    /// </summary>
    [Description("Microsoft Store change")]
    MsChange = 50,
}
