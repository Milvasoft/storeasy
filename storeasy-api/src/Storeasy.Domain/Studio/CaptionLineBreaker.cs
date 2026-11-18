using System.Globalization;
using System.Text;

namespace Storeasy.Domain.Studio;

/// <summary>
/// Greedy line breaking of captions, independent of the font engine: the caller measures. Break opportunities are spaces,
/// a hyphen inside a word, and any boundary next to a Chinese or Japanese character, except before closing punctuation
/// and small kana and after opening brackets (basic kinsoku). Korean breaks at spaces like Latin text. A piece that does
/// not fit on a line on its own (a long word, Thai text without spaces) is split between grapheme clusters, so combining
/// marks never leave their base letter. A new line in the text is a manual break.
/// </summary>
public static class CaptionLineBreaker
{
    private const string _noLineStart = "、。，．・：；？！ー’”）〕］｝〉》」』】〟"
                                        + "ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ々"
                                        + "!%),.:;?]}";

    private const string _noLineEnd = "‘“（〔［｛〈《「『【〝([{";

    /// <summary>
    /// Breaks <paramref name="text"/> into lines no wider than <paramref name="maxWidth"/> as measured by
    /// <paramref name="measure"/>. Lines are trimmed; an empty text gives no lines.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="maxWidth"></param>
    /// <param name="measure">Width of a string in the caller's units.</param>
    /// <returns></returns>
    public static List<string> Break(string text, float maxWidth, Func<string, float> measure)
    {
        var lines = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return lines;

        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            BreakParagraph(paragraph.Trim(), maxWidth, measure, lines);

        return lines;
    }

    /// <summary>
    /// Pieces of <paramref name="paragraph"/> that end at a break opportunity (each keeps its trailing spaces).
    /// </summary>
    /// <param name="paragraph"></param>
    /// <returns></returns>
    public static List<string> Segment(string paragraph)
    {
        var elements = TextElements(paragraph);
        var segments = new List<string>();
        var current = new StringBuilder();

        for (var i = 0; i < elements.Count; i++)
        {
            current.Append(elements[i]);

            if (i + 1 < elements.Count && CanBreakBetween(elements[i], elements[i + 1], i > 0 ? elements[i - 1] : null))
            {
                segments.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
            segments.Add(current.ToString());

        return segments;
    }

    /// <summary>
    /// Whether <paramref name="element"/> starts with a Chinese or Japanese character (Han ideographs, kana, CJK symbols,
    /// full width forms), the characters that may break anywhere.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public static bool IsIdeographic(string element)
    {
        if (string.IsNullOrEmpty(element))
            return false;

        var codePoint = char.ConvertToUtf32(element, 0);

        return codePoint is (>= 0x2E80 and <= 0x2FDF) or (>= 0x3000 and <= 0x30FF) or (>= 0x31F0 and <= 0x31FF) or (>= 0x3400 and <= 0x4DBF)
                         or (>= 0x4E00 and <= 0x9FFF) or (>= 0xF900 and <= 0xFAFF) or (>= 0xFF00 and <= 0xFFEF) or (>= 0x20000 and <= 0x3134F);
    }

    private static void BreakParagraph(string paragraph, float maxWidth, Func<string, float> measure, List<string> lines)
    {
        if (paragraph.Length == 0)
            return;

        var line = string.Empty;

        foreach (var segment in Segment(paragraph))
        {
            var candidate = line + segment;

            if (measure(candidate.TrimEnd()) <= maxWidth)
            {
                line = candidate;
                continue;
            }

            if (line.Length > 0)
                lines.Add(line.TrimEnd());

            line = string.Empty;

            if (measure(segment.TrimEnd()) <= maxWidth)
            {
                line = segment;
                continue;
            }

            // The piece alone is too wide: split it between grapheme clusters.
            foreach (var element in TextElements(segment))
            {
                if (line.Length > 0 && measure((line + element).TrimEnd()) > maxWidth)
                {
                    lines.Add(line.TrimEnd());
                    line = string.Empty;
                }

                line += element;
            }
        }

        if (line.Trim().Length > 0)
            lines.Add(line.TrimEnd());
    }

    private static bool CanBreakBetween(string before, string after, string beforeBefore)
    {
        if (string.IsNullOrWhiteSpace(after))
            return false;

        if (_noLineStart.Contains(after[0]) || _noLineEnd.Contains(before[^1]))
            return false;

        if (char.IsWhiteSpace(before[^1]))
            return true;

        if (before == "-" && beforeBefore != null && char.IsLetterOrDigit(beforeBefore[^1]) && char.IsLetterOrDigit(after[0]))
            return true;

        return IsIdeographic(before) || IsIdeographic(after);
    }

    private static List<string> TextElements(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);

        while (enumerator.MoveNext())
            elements.Add(enumerator.GetTextElement());

        return elements;
    }
}
