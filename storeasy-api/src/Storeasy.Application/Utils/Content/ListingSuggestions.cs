using Storeasy.Domain.Content;
using Storeasy.Domain.Rules;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// Which listing fields get AI suggestions and how a suggestion is fitted into the catalog limits. Only the free text
/// fields of a listing: links, contact details and the EULA are facts the model cannot know.
/// </summary>
public static class ListingSuggestions
{
    /// <summary>
    /// Fields with suggestions, per store.
    /// </summary>
    public static IReadOnlyDictionary<StoreKind, string[]> Fields { get; } = new Dictionary<StoreKind, string[]>
    {
        [StoreKind.Apple] = ["name", "subtitle", "description", "keywords", "promotionalText", "whatsNew"],
        [StoreKind.Google] = ["title", "shortDescription", "fullDescription"],
    };

    /// <summary>
    /// The listing field of <paramref name="field"/> when it gets suggestions; otherwise null.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public static ListingField Find(StoreKind store, string field)
    {
        if (string.IsNullOrWhiteSpace(field) || !Fields.TryGetValue(store, out var fields) || !fields.Contains(field, StringComparer.Ordinal))
            return null;

        return ListingFields.Find(store, field);
    }

    /// <summary>
    /// The limits of a field from the rules catalog: characters, bytes, the minimum (characters, or per keyword).
    /// </summary>
    /// <param name="field"></param>
    /// <param name="catalog"></param>
    /// <returns></returns>
    public static (int? MaxLength, int? MaxBytes, int? MinLength) Limits(ListingField field, RuleCatalog catalog)
    {
        var parameters = catalog.Find(field.LimitRuleId)?.Params;

        if (parameters == null)
            return (null, null, null);

        var minimum = parameters.MinChars;

        if (minimum == null && parameters.Extra != null && parameters.Extra.TryGetValue("minCharsPerKeyword", out var perKeyword) && perKeyword.ValueKind == JsonValueKind.Number)
            minimum = perKeyword.GetInt32();

        return (parameters.MaxChars, parameters.MaxBytes, minimum);
    }

    /// <summary>
    /// The catalog rules of a field as prompt lines (English title and description).
    /// </summary>
    /// <param name="field"></param>
    /// <param name="catalog"></param>
    /// <returns></returns>
    public static List<string> RuleLines(ListingField field, RuleCatalog catalog)
    {
        var rule = catalog.Find(field.LimitRuleId);

        if (rule == null)
            return [];

        var text = string.Join(" ", new[] { rule.Title?.En, rule.Description?.En }.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));

        return string.IsNullOrWhiteSpace(text) ? [] : [text];
    }

    /// <summary>
    /// <paramref name="value"/> within the field's limits, and whether it had to be shortened. Keywords lose whole keywords
    /// from the end (and their duplicates and spaces); other texts are cut at the last sentence or word that fits. Null when
    /// nothing usable is left.
    /// </summary>
    /// <param name="field"></param>
    /// <param name="value"></param>
    /// <param name="maxLength"></param>
    /// <param name="maxBytes"></param>
    /// <returns></returns>
    public static (string Value, bool Shortened)? Fit(string field, string value, int? maxLength, int? maxBytes)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
            return null;

        if (field == "keywords")
            text = string.Join(",", text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase));

        if (Fits(text, maxLength, maxBytes))
            return (text, !string.Equals(text, value.Trim(), StringComparison.Ordinal));

        if (field == "keywords")
        {
            var keywords = text.Split(',').ToList();

            while (keywords.Count > 1 && !Fits(string.Join(",", keywords), maxLength, maxBytes))
                keywords.RemoveAt(keywords.Count - 1);

            var kept = string.Join(",", keywords);

            return Fits(kept, maxLength, maxBytes) ? (kept, true) : null;
        }

        var info = new StringInfo(text);
        var count = Math.Min(info.LengthInTextElements, maxLength ?? int.MaxValue);
        var head = info.SubstringByTextElements(0, count);

        while (head.Length > 0 && !Fits(head, maxLength, maxBytes))
        {
            var shorter = new StringInfo(head);
            head = shorter.SubstringByTextElements(0, shorter.LengthInTextElements - 1);
        }

        var sentence = LastBreak(head, ['.', '!', '?', '\n']);
        // A head that ends where a word ends keeps that word.
        var wordEnds = head.Length < text.Length && char.IsWhiteSpace(text[head.Length]);
        var cut = sentence > head.Length / 2 ? head[..(sentence + 1)] : wordEnds ? head : LastBreak(head, [' ']) is var space and > 0 ? head[..space] : head;

        cut = cut.TrimEnd(' ', ',', ';', ':', '-', '\n', '\r');

        return cut.Length == 0 ? null : (cut, true);
    }

    /// <summary>
    /// The app's catalog profile (Profile tab) as short prompt lines.
    /// </summary>
    /// <param name="profile"></param>
    /// <returns></returns>
    public static List<string> ProfileLines(AppProfile profile)
    {
        if (profile == null)
            return [];

        var lines = new List<string>();

        void Add(string label, IEnumerable<string> values)
        {
            var list = values?.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

            if (list is { Count: > 0 })
                lines.Add($"{label}: {string.Join(", ", list)}");
        }

        Add("Platforms", profile.Platforms);
        Add("Device families", profile.DeviceFamilies);
        Add("App type", [profile.AppType]);
        Add("Features", profile.Features);
        Add("Regions", profile.Regions);

        if (profile.IsLive is { } live)
            lines.Add(live ? "Already live in the store" : "Not live in the store yet");

        return lines;
    }

    /// <summary>
    /// The message key of a failed AI suggestion call.
    /// </summary>
    /// <param name="outcome"></param>
    /// <returns></returns>
    public static string FailureKey(ReplyDraftOutcome outcome) => outcome switch
    {
        ReplyDraftOutcome.KeyRejected => MessageKey.AiKeyRejected,
        ReplyDraftOutcome.RateLimited => MessageKey.AiRateLimited,
        ReplyDraftOutcome.Unavailable => MessageKey.AiUnavailable,
        ReplyDraftOutcome.Refused => MessageKey.AiSuggestionRefused,
        _ => MessageKey.AiSuggestionFailed,
    };

    private static bool Fits(string text, int? maxLength, int? maxBytes)
        => (maxLength == null || ContentDiff.CharCount(text) <= maxLength) && (maxBytes == null || ContentDiff.ByteCount(text) <= maxBytes);

    private static int LastBreak(string text, char[] breaks)
    {
        for (var index = text.Length - 1; index > 0; index--)
        {
            if (breaks.Contains(text[index]))
                return index;
        }

        return -1;
    }
}
