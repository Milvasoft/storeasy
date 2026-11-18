using Storeasy.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storeasy.Domain.Apps;

/// <summary>
/// The typed JSON documents of saved answers, one per <see cref="SavedAnswerKind"/>. Parsing is strict (unknown members
/// are refused), so a typo never silently drops an answer.
/// </summary>
public static class SavedAnswerDocuments
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Document type of <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static Type TypeOf(SavedAnswerKind kind) => kind switch
    {
        SavedAnswerKind.PrivacyLabels => typeof(PrivacyLabelsAnswers),
        SavedAnswerKind.DataSafety => typeof(DataSafetyAnswers),
        SavedAnswerKind.ContentRating => typeof(ContentRatingAnswers),
        SavedAnswerKind.TargetAudience => typeof(TargetAudienceAnswers),
        SavedAnswerKind.ReviewLogin => typeof(ReviewLoginAnswers),
        SavedAnswerKind.ReviewNotes => typeof(ReviewNotesAnswers),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// Parses and normalizes <paramref name="json"/> as the document of <paramref name="kind"/>. Returns null (and the
    /// reason) when it does not fit.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="json"></param>
    /// <param name="error"></param>
    /// <returns>The canonical JSON of the document.</returns>
    public static string Normalize(SavedAnswerKind kind, string json, out string error)
    {
        error = null;

        try
        {
            var document = JsonSerializer.Deserialize(json, TypeOf(kind), _options);

            if (document == null)
            {
                error = "empty document";
                return null;
            }

            return JsonSerializer.Serialize(document, TypeOf(kind), _options);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Reads a stored document; null when there is none or it does not parse.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="json"></param>
    /// <returns></returns>
    public static T Read<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// App Store privacy nutrition labels (no API: an assisted step).
/// </summary>
public sealed class PrivacyLabelsAnswers
{
    /// <summary>
    /// Whether the app or its third parties collect any data.
    /// </summary>
    public bool CollectsData { get; set; }

    /// <summary>
    /// Collected data types.
    /// </summary>
    public List<PrivacyDataTypeAnswer> DataTypes { get; set; } = [];

    /// <summary>
    /// Privacy policy address.
    /// </summary>
    public string PrivacyPolicyUrl { get; set; }
}

/// <summary>
/// One collected data type of the App Privacy labels.
/// </summary>
public sealed class PrivacyDataTypeAnswer
{
    /// <summary>
    /// Category (Contact Info, Identifiers, Usage Data...).
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Data type (Email Address, User ID, Product Interaction...).
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Purposes (App Functionality, Analytics...).
    /// </summary>
    public List<string> Purposes { get; set; } = [];

    /// <summary>
    /// Whether it is linked to the user's identity.
    /// </summary>
    public bool LinkedToUser { get; set; }

    /// <summary>
    /// Whether it is used for tracking.
    /// </summary>
    public bool UsedForTracking { get; set; }
}

/// <summary>
/// Google Play Data safety answers, kept as the machine readable rows of Play's own CSV (question id, response id, value),
/// so the CSV sent through <c>applications.dataSafety</c> is generated exactly. Export the CSV once from Play Console to
/// start from the app's current answers.
/// </summary>
public sealed class DataSafetyAnswers
{
    /// <summary>
    /// Rows of the form.
    /// </summary>
    public List<DataSafetyRow> Rows { get; set; } = [];
}

/// <summary>
/// One row of the Data safety CSV.
/// </summary>
public sealed class DataSafetyRow
{
    /// <summary>
    /// Question ID (machine readable).
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Response ID (machine readable); empty for free text questions.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Response value (true, false, or text).
    /// </summary>
    public string ResponseValue { get; set; }

    /// <summary>
    /// Human friendly question label (kept for readability, ignored by Play).
    /// </summary>
    public string Label { get; set; }
}

/// <summary>
/// Play content rating questionnaire (IARC) answers.
/// </summary>
public sealed class ContentRatingAnswers
{
    /// <summary>
    /// Questionnaire category (e.g. "Social Networking, Forums, Blogs and UGC Sharing").
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Contact e-mail for the rating authorities.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Answers by question.
    /// </summary>
    public Dictionary<string, string> Answers { get; set; } = [];
}

/// <summary>
/// Play target audience and content answers.
/// </summary>
public sealed class TargetAudienceAnswers
{
    /// <summary>
    /// Target age groups (e.g. "18+").
    /// </summary>
    public List<string> AgeGroups { get; set; } = [];

    /// <summary>
    /// Whether the app may unintentionally appeal to children.
    /// </summary>
    public bool AppealsToChildren { get; set; }

    /// <summary>
    /// Whether the app contains ads.
    /// </summary>
    public bool ContainsAds { get; set; }
}

/// <summary>
/// App Review contact and demo account. The password is not part of the document; it is sealed separately.
/// </summary>
public sealed class ReviewLoginAnswers
{
    /// <summary>
    /// Contact first name.
    /// </summary>
    public string ContactFirstName { get; set; }

    /// <summary>
    /// Contact last name.
    /// </summary>
    public string ContactLastName { get; set; }

    /// <summary>
    /// Contact e-mail.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Contact phone (international format).
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Whether the reviewer must sign in.
    /// </summary>
    public bool DemoAccountRequired { get; set; }

    /// <summary>
    /// Demo account user name.
    /// </summary>
    public string DemoAccountName { get; set; }
}

/// <summary>
/// Notes for the reviewer.
/// </summary>
public sealed class ReviewNotesAnswers
{
    /// <summary>
    /// Notes.
    /// </summary>
    public string Notes { get; set; }
}
