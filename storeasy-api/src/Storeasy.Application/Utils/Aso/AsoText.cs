using System.Globalization;
using System.Text;

namespace Storeasy.Application.Utils.Aso;

/// <summary>
/// Text rules of ASO keywords: how a keyword is normalized (so "Photo  Editor" and "photo editor" are one keyword) and
/// how texts are split into words. Lower casing uses the language's culture, so Turkish dotted and dotless i stay correct.
/// </summary>
public static class AsoText
{
    /// <summary>
    /// The culture of a language or locale code (<c>tr</c>, <c>tr-TR</c>, <c>en-US</c>); the invariant culture when unknown.
    /// </summary>
    /// <param name="languageOrLocale"></param>
    /// <returns></returns>
    public static CultureInfo Culture(string languageOrLocale)
    {
        if (string.IsNullOrWhiteSpace(languageOrLocale))
            return CultureInfo.InvariantCulture;

        try
        {
            return CultureInfo.GetCultureInfo(languageOrLocale.Trim().Replace('_', '-'));
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// The keyword trimmed, with one space between words and lower case in <paramref name="culture"/>; null when nothing
    /// is left. Separators other than letters and digits (commas, dashes, quotes) become spaces.
    /// </summary>
    /// <param name="keyword"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public static string Normalize(string keyword, CultureInfo culture)
    {
        var words = Words(keyword, culture);

        return words.Count == 0 ? null : string.Join(' ', words);
    }

    /// <summary>
    /// The words of <paramref name="text"/> in lower case (letters and digits; everything else separates words).
    /// </summary>
    /// <param name="text"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public static List<string> Words(string text, CultureInfo culture)
    {
        var words = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return words;

        var builder = new StringBuilder();

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character) || char.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
            {
                builder.Append(character);
                continue;
            }

            Flush(builder, words, culture);
        }

        Flush(builder, words, culture);

        return words;
    }

    /// <summary>
    /// The prefix of <paramref name="term"/> with <paramref name="length"/> characters, never ending inside a surrogate pair.
    /// </summary>
    /// <param name="term"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static string Prefix(string term, int length)
    {
        if (term == null || length >= term.Length)
            return term;

        if (length > 0 && char.IsHighSurrogate(term[length - 1]))
            length++;

        return term[..length].TrimEnd();
    }

    private static void Flush(StringBuilder builder, List<string> words, CultureInfo culture)
    {
        if (builder.Length == 0)
            return;

        words.Add(builder.ToString().ToLower(culture));
        builder.Clear();
    }
}
