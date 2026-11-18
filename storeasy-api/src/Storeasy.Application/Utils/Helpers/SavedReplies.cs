using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Saved reply rules: one variant per locale, known variables only.
/// </summary>
public static partial class SavedReplies
{
    /// <summary>
    /// Variables a saved reply may use (Turkish and English names).
    /// </summary>
    public static IReadOnlySet<string> Variables { get; } = new HashSet<string>(StringComparer.Ordinal) { "ad", "name", "uygulama", "app", new string(['s', 'ü', 'r', 'ü', 'm']), "version" };

    /// <summary>
    /// Whether a variant has a locale and a text within the limits.
    /// </summary>
    public static bool IsValidVariant(LocalizedText variant)
        => variant != null
           && !string.IsNullOrWhiteSpace(variant.Locale) && variant.Locale.Trim().Length <= 20
           && !string.IsNullOrWhiteSpace(variant.Text) && variant.Text.Length <= Domain.Feedback.SavedReply.BodyMaxLength;

    /// <summary>
    /// Variables in <paramref name="text"/> that are not in <see cref="Variables"/>.
    /// </summary>
    public static List<string> UnknownVariables(string text)
        => [.. VariableRegex().Matches(text ?? string.Empty).Select(m => m.Groups["name"].Value).Where(name => !Variables.Contains(name)).Distinct()];

    /// <summary>
    /// Trims the variants and keeps one per locale (the last one wins).
    /// </summary>
    public static List<LocalizedText> Normalize(IEnumerable<LocalizedText> variants)
        => [.. (variants ?? []).Where(v => v != null)
                               .Select(v => new LocalizedText { Locale = v.Locale.Trim(), Text = v.Text.Trim() })
                               .GroupBy(v => v.Locale, StringComparer.OrdinalIgnoreCase)
                               .Select(g => g.Last())];

    [GeneratedRegex(@"\{(?<name>[^{}\s]+)\}")]
    private static partial Regex VariableRegex();
}
