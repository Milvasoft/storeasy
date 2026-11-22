using System.Globalization;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Firefox;

/// <summary>
/// Version strings and ids of Firefox add-ons. A version is one to four dot separated numbers of at most nine digits without
/// leading zeros (the format Firefox and AMO require for new uploads); older versions may carry letters (<c>1.0a1</c>) and
/// are still ordered, number part by number part.
/// </summary>
public static partial class FirefoxVersions
{
    /// <summary>
    /// Longest guid AMO takes.
    /// </summary>
    public const int GuidMaxLength = 255;

    /// <summary>
    /// Whether <paramref name="version"/> has the format AMO takes for a new upload.
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    public static bool IsValid(string version) => version != null && VersionPattern().IsMatch(version);

    /// <summary>
    /// Compares two versions (number parts as numbers, a missing part is 0, letters after a number sort before the plain
    /// number: <c>1.0a1</c> is below <c>1.0</c>).
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static int Compare(string left, string right)
    {
        var a = (left ?? string.Empty).Split('.');
        var b = (right ?? string.Empty).Split('.');

        for (var index = 0; index < Math.Max(a.Length, b.Length); index++)
        {
            var (numberA, restA) = Part(index < a.Length ? a[index] : "0");
            var (numberB, restB) = Part(index < b.Length ? b[index] : "0");

            if (numberA != numberB)
                return numberA.CompareTo(numberB);

            if (restA != restB)
            {
                // A part with a pre-release suffix sorts before the same number without one.
                if (restA.Length == 0)
                    return 1;

                if (restB.Length == 0)
                    return -1;

                return CompareSuffix(restA, restB);
            }
        }

        return 0;
    }

    /// <summary>
    /// Whether <paramref name="guid"/> is an add-on id AMO takes: an e-mail like id (<c>name@example.com</c>) or a braced
    /// UUID.
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    public static bool IsGuid(string guid)
        => !string.IsNullOrEmpty(guid) && guid.Length <= GuidMaxLength && (EmailIdPattern().IsMatch(guid) || UuidIdPattern().IsMatch(guid));

    /// <summary>
    /// Whether <paramref name="value"/> names an add-on: a numeric id, a guid or a slug.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsAddonReference(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= GuidMaxLength && (IsGuid(value) || SlugPattern().IsMatch(value));

    // A pre-release suffix is letters and a number (b2, a10, pre1): letters first, then the number.
    private static int CompareSuffix(string left, string right)
    {
        var lettersA = new string([.. left.TakeWhile(c => !char.IsAsciiDigit(c))]);
        var lettersB = new string([.. right.TakeWhile(c => !char.IsAsciiDigit(c))]);
        var byLetters = string.CompareOrdinal(lettersA, lettersB);

        if (byLetters != 0)
            return byLetters;

        var (numberA, restA) = Part(left[lettersA.Length..]);
        var (numberB, restB) = Part(right[lettersB.Length..]);

        return numberA != numberB ? numberA.CompareTo(numberB) : string.CompareOrdinal(restA, restB);
    }

    private static (long Number, string Suffix) Part(string part)
    {
        var digits = 0;

        while (digits < part.Length && digits < 18 && char.IsAsciiDigit(part[digits]))
            digits++;

        var number = digits == 0 ? 0 : long.Parse(part[..digits], NumberStyles.None, CultureInfo.InvariantCulture);

        return (number, part[digits..]);
    }

    [GeneratedRegex(@"^(0|[1-9][0-9]{0,8})(\.(0|[1-9][0-9]{0,8})){0,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9\-._]*@[a-zA-Z0-9\-._]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailIdPattern();

    [GeneratedRegex(@"^\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$", RegexOptions.CultureInvariant)]
    private static partial Regex UuidIdPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9\-_~]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
