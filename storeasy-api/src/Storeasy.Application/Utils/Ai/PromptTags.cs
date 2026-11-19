using System.Text;

namespace Storeasy.Application.Utils.Ai;

/// <summary>
/// Writing data into a prompt: every value goes inside a named tag and is escaped so its text cannot open or close a tag.
/// Prompts use line feeds only, so they are the same bytes on every platform and checkout.
/// </summary>
public static class PromptTags
{
    /// <summary>
    /// Line feed (char 10).
    /// </summary>
    public const char LineFeed = (char)10;

    /// <summary>
    /// <paramref name="text"/> with the characters that could open or close a tag escaped.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string Escape(string text) => text?.Replace("&", "&amp;", StringComparison.Ordinal)
                                                     .Replace("<", "&lt;", StringComparison.Ordinal)
                                                     .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>
    /// Appends <c>&lt;name&gt;value&lt;/name&gt;</c> and a line feed; nothing when the value is empty. The value is trimmed,
    /// cut to <paramref name="maxLength"/> characters (with a note) and escaped; its line ends become line feeds.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <param name="maxLength">Longest value kept; 0 for no limit.</param>
    public static void Tag(StringBuilder builder, string name, string value, int maxLength = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.Append('<').Append(name).Append('>').Append(Escape(Cut(value.Trim(), maxLength).ReplaceLineEndings(LineFeed.ToString()))).Append("</").Append(name).Append('>').Append(LineFeed);
    }

    /// <summary>
    /// <paramref name="value"/> cut to <paramref name="maxLength"/> characters with a note of the original length.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="maxLength">0 for no limit.</param>
    /// <returns></returns>
    public static string Cut(string value, int maxLength)
    {
        if (value == null || maxLength <= 0 || value.Length <= maxLength)
            return value;

        return $"{value[..maxLength]} [cut, {value.Length} characters in total]";
    }
}
