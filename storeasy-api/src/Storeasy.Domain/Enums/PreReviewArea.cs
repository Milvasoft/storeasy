using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Area of a pre-review finding. Values are explicit and never reused.
/// </summary>
public enum PreReviewArea : byte
{
    /// <summary>Names, descriptions, keywords, URLs, categories.</summary>
    [Description("Metadata")]
    Metadata = 1,

    /// <summary>Screenshots and previews.</summary>
    [Description("Screenshots")]
    Screenshots = 2,

    /// <summary>Privacy labels, data safety, privacy policy, tracking.</summary>
    [Description("Privacy")]
    Privacy = 3,

    /// <summary>Age rating and target audience.</summary>
    [Description("Age rating")]
    AgeRating = 4,

    /// <summary>In-app purchases, subscriptions, pricing.</summary>
    [Description("Payments")]
    Payments = 5,

    /// <summary>Whether the app does what the listing says (as far as the listing shows).</summary>
    [Description("Functionality as described")]
    Functionality = 6,

    /// <summary>Legal: intellectual property, regional law, export compliance.</summary>
    [Description("Legal")]
    Legal = 7,

    /// <summary>Anything else.</summary>
    [Description("Other")]
    Other = 8,
}
