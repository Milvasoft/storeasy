using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kind of a saved answer document of an app.
/// </summary>
public enum SavedAnswerKind : byte
{
    /// <summary>
    /// App Store privacy nutrition labels.
    /// </summary>
    [Description("App Privacy labels")]
    PrivacyLabels = 1,

    /// <summary>
    /// Google Play Data safety form.
    /// </summary>
    [Description("Data safety")]
    DataSafety = 2,

    /// <summary>
    /// Play content rating questionnaire.
    /// </summary>
    [Description("Content rating")]
    ContentRating = 3,

    /// <summary>
    /// Play target audience and content.
    /// </summary>
    [Description("Target audience")]
    TargetAudience = 4,

    /// <summary>
    /// Contact and demo account for App Review (password sealed).
    /// </summary>
    [Description("Review login")]
    ReviewLogin = 5,

    /// <summary>
    /// Notes for the reviewer.
    /// </summary>
    [Description("Review notes")]
    ReviewNotes = 6,
}
