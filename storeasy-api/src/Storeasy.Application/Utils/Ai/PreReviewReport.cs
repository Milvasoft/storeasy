using Storeasy.Domain.Apps;
using System.Text.Json;

namespace Storeasy.Application.Utils.Ai;

/// <summary>
/// The report of an AI pre-review, read from the model's JSON answer and checked: known verdict, severity and area values,
/// a likelihood clamped to 0 to 100, text cut to the stored lengths, empty items dropped, findings ordered by severity.
/// </summary>
public sealed class PreReviewReport
{
    /// <summary>
    /// Most findings kept.
    /// </summary>
    public const int MaxFindings = 25;

    /// <summary>
    /// Most runtime checks kept.
    /// </summary>
    public const int MaxRuntimeChecks = 10;

    /// <summary>
    /// Most positive notes kept.
    /// </summary>
    public const int MaxPositiveNotes = 5;

    /// <summary>
    /// Longest text of one field of a finding or one list item.
    /// </summary>
    public const int ItemMaxLength = 2000;

    /// <summary>
    /// Expected decision.
    /// </summary>
    public PreReviewVerdict Verdict { get; set; }

    /// <summary>
    /// Estimated chance of approval, 0 to 100.
    /// </summary>
    public int ApprovalLikelihood { get; set; }

    /// <summary>
    /// Short summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Problems found, most severe first.
    /// </summary>
    public List<PreReviewFinding> Findings { get; set; } = [];

    /// <summary>
    /// What a reviewer will test while running the app.
    /// </summary>
    public List<string> RuntimeChecks { get; set; } = [];

    /// <summary>
    /// What is already good.
    /// </summary>
    public List<string> PositiveNotes { get; set; } = [];

    /// <summary>
    /// Reads and checks the model's answer. Returns null when it is not a report: not JSON, an unknown verdict, no
    /// likelihood or no summary. A finding with an unknown severity or area is kept as minor or other.
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    public static PreReviewReport Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var verdict = ParseVerdict(Text(root, "verdict"));
            var summary = Clean(Text(root, "summary"), PreReview.SummaryMaxLength);

            if (verdict == null || summary == null || !root.TryGetProperty("approval_likelihood", out var likelihood) || likelihood.ValueKind != JsonValueKind.Number)
                return null;

            var findings = Array(root, "findings").Where(f => f.ValueKind == JsonValueKind.Object)
                                                 .Select(Finding)
                                                 .Where(f => f.Title != null || f.Fix != null)
                                                 .OrderBy(f => f.Severity)
                                                 .Take(MaxFindings)
                                                 .ToList();

            return new PreReviewReport
            {
                Verdict = verdict.Value,
                ApprovalLikelihood = (int)Math.Clamp(Math.Round(likelihood.GetDouble()), 0, 100),
                Summary = summary,
                Findings = findings,
                RuntimeChecks = Strings(root, "runtime_checks", MaxRuntimeChecks),
                PositiveNotes = Strings(root, "positive_notes", MaxPositiveNotes),
            };
        }
    }

    private static PreReviewFinding Finding(JsonElement element) => new()
    {
        Severity = Text(element, "severity") switch
        {
            "blocker" => PreReviewSeverity.Blocker,
            "major" => PreReviewSeverity.Major,
            _ => PreReviewSeverity.Minor,
        },
        Area = Text(element, "area") switch
        {
            "metadata" => PreReviewArea.Metadata,
            "screenshots" => PreReviewArea.Screenshots,
            "privacy" => PreReviewArea.Privacy,
            "age_rating" => PreReviewArea.AgeRating,
            "payments" => PreReviewArea.Payments,
            "functionality" => PreReviewArea.Functionality,
            "legal" => PreReviewArea.Legal,
            _ => PreReviewArea.Other,
        },
        Guideline = Clean(Text(element, "guideline"), ItemMaxLength),
        Title = Clean(Text(element, "title"), ItemMaxLength),
        Evidence = Clean(Text(element, "evidence"), ItemMaxLength),
        Fix = Clean(Text(element, "fix"), ItemMaxLength),
    };

    private static PreReviewVerdict? ParseVerdict(string value) => value switch
    {
        "likely_approved" => PreReviewVerdict.LikelyApproved,
        "at_risk" => PreReviewVerdict.AtRisk,
        "likely_rejected" => PreReviewVerdict.LikelyRejected,
        _ => null,
    };

    private static List<string> Strings(JsonElement root, string name, int max)
        => [.. Array(root, name).Select(e => e.ValueKind == JsonValueKind.String ? Clean(e.GetString(), ItemMaxLength) : null).Where(s => s != null).Take(max)];

    private static IEnumerable<JsonElement> Array(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Clean(string value, int maxLength)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
