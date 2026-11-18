using System.Net;
using System.Text.RegularExpressions;

namespace Storeasy.Infrastructure.Rules;

/// <summary>
/// Turns a page (HTML, RSS or text) into stable lines of text for hashing and diffing: scripts, styles and markup are
/// dropped, entities decoded, whitespace collapsed, one block per line.
/// </summary>
public static partial class ContentNormalizer
{
    /// <summary>
    /// The normalized text of <paramref name="content"/>, at most <paramref name="maxLength"/> characters.
    /// </summary>
    public static string Normalize(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var text = DroppedRegex().Replace(content, " ");

        text = BlockRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, " ");
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');

        var lines = text.Split('\n').Select(line => SpaceRegex().Replace(line, " ").Trim()).Where(line => line.Length > 0);
        var normalized = string.Join('\n', lines);

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    [GeneratedRegex(@"<(script|style|noscript|svg|template)\b[^>]*>.*?</\1\s*>|<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DroppedRegex();

    [GeneratedRegex(@"<\s*(br|/p|/div|/li|/h[1-6]|/tr|/item|/entry|/title|/section|/article|/td|/th)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();
}
