using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// TestFlight beta app review state of a build. Values are explicit and never reused.
/// </summary>
public enum BetaReviewState : byte
{
    /// <summary>Never submitted.</summary>
    [Description("Not submitted")]
    NotSubmitted = 1,

    /// <summary>Waiting for review.</summary>
    [Description("Waiting for review")]
    WaitingForReview = 2,

    /// <summary>In review.</summary>
    [Description("In review")]
    InReview = 3,

    /// <summary>Approved; external groups can test it.</summary>
    [Description("Approved")]
    Approved = 4,

    /// <summary>Rejected.</summary>
    [Description("Rejected")]
    Rejected = 5,
}
