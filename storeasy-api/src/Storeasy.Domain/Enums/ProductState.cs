using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Store neutral state of a store product (the store's own state text is kept beside it).
/// </summary>
public enum ProductState : byte
{
    /// <summary>
    /// Created but never activated (Play draft).
    /// </summary>
    [Description("Draft")]
    Draft = 1,

    /// <summary>
    /// Metadata (localization, price, review screenshot) is missing.
    /// </summary>
    [Description("Missing metadata")]
    MissingMetadata = 2,

    /// <summary>
    /// Complete and ready to be submitted.
    /// </summary>
    [Description("Ready to submit")]
    ReadyToSubmit = 3,

    /// <summary>
    /// Submitted, waiting for App Review.
    /// </summary>
    [Description("Waiting for review")]
    WaitingForReview = 4,

    /// <summary>
    /// In App Review.
    /// </summary>
    [Description("In review")]
    InReview = 5,

    /// <summary>
    /// The store asks the developer to act (developer action needed, pending binary approval).
    /// </summary>
    [Description("Action needed")]
    ActionNeeded = 6,

    /// <summary>
    /// Approved and on sale (App Store).
    /// </summary>
    [Description("Approved")]
    Approved = 7,

    /// <summary>
    /// Active and on sale (Play).
    /// </summary>
    [Description("Active")]
    Active = 8,

    /// <summary>
    /// Deactivated; existing buyers keep it (Play).
    /// </summary>
    [Description("Inactive")]
    Inactive = 9,

    /// <summary>
    /// Rejected by App Review.
    /// </summary>
    [Description("Rejected")]
    Rejected = 10,

    /// <summary>
    /// Removed from sale.
    /// </summary>
    [Description("Removed")]
    Removed = 11,
}
