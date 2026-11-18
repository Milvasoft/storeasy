using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Reads the App Review guidelines cited in a pasted rejection text ("Guideline 5.1.1(v) - Legal - Privacy - ...") and
/// turns them into checklist items. The e-mail App Store sends ("There's an issue with your submission") cites none: the
/// caller then warns that the Resolution Center message is the one to paste.
/// </summary>
public static partial class GuidelineParser
{
    /// <summary>
    /// Longest guideline title kept.
    /// </summary>
    public const int TitleMaxLength = 200;

    /// <summary>
    /// The cited guidelines in order of first appearance, each once. The number is kept without its sub clause
    /// ("5.1.1(v)" becomes "5.1.1"); the title is the text after the dash on the same line, if any.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static List<RejectionGuideline> Parse(string text)
    {
        var guidelines = new List<RejectionGuideline>();

        if (string.IsNullOrWhiteSpace(text))
            return guidelines;

        foreach (Match match in GuidelineRegex().Matches(text))
        {
            var code = match.Groups["code"].Value.TrimEnd('.');

            if (guidelines.Any(g => g.Code == code))
                continue;

            var title = match.Groups["title"].Success ? match.Groups["title"].Value.Trim() : null;

            guidelines.Add(new RejectionGuideline
            {
                Code = code,
                Title = string.IsNullOrWhiteSpace(title) ? null : title.Length > TitleMaxLength ? title[..TitleMaxLength] : title,
            });
        }

        return guidelines;
    }

    /// <summary>
    /// One checklist item per guideline (its text is the guideline title or the number), keeping the items of
    /// <paramref name="existing"/> whose guideline is still cited and every item added by hand.
    /// </summary>
    /// <param name="guidelines"></param>
    /// <param name="existing"></param>
    /// <returns></returns>
    public static List<RejectionChecklistItem> BuildChecklist(IReadOnlyList<RejectionGuideline> guidelines, IReadOnlyList<RejectionChecklistItem> existing)
    {
        var kept = (existing ?? []).Where(item => item.GuidelineCode == null || guidelines.Any(g => g.Code == item.GuidelineCode)).ToList();

        foreach (var guideline in guidelines.Where(g => !kept.Any(item => item.GuidelineCode == g.Code)))
        {
            kept.Add(new RejectionChecklistItem
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                GuidelineCode = guideline.Code,
                Text = guideline.Title ?? $"Guideline {guideline.Code}",
                Status = ChecklistItemStatus.Todo,
            });
        }

        return kept;
    }

    [GeneratedRegex(@"Guideline\s+(?<code>\d+(?:\.\d+){0,3})(?:\s*\([a-z0-9]+\))*(?:[ \t]*[-:][ \t]*(?<title>[^\r\n]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex GuidelineRegex();
}
