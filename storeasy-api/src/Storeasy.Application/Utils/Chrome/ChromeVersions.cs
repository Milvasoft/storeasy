using System.Globalization;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Chrome;

/// <summary>
/// Extension ids and package versions as the Chrome Web Store reads them.
/// </summary>
public static partial class ChromeVersions
{
    /// <summary>
    /// Highest value of one part of a version.
    /// </summary>
    public const int MaxPart = 65535;

    [GeneratedRegex("^[a-p]{32}$")]
    private static partial Regex ItemIdRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,100}$")]
    private static partial Regex PublisherIdRegex();

    /// <summary>
    /// Whether <paramref name="itemId"/> is an extension id (32 letters from a to p).
    /// </summary>
    /// <param name="itemId"></param>
    /// <returns></returns>
    public static bool IsItemId(string itemId) => itemId != null && ItemIdRegex().IsMatch(itemId);

    /// <summary>
    /// Whether <paramref name="publisherId"/> looks like a publisher id (letters, digits, <c>-</c> and <c>_</c>).
    /// </summary>
    /// <param name="publisherId"></param>
    /// <returns></returns>
    public static bool IsPublisherId(string publisherId) => publisherId != null && PublisherIdRegex().IsMatch(publisherId);

    /// <summary>
    /// The parts of a manifest version: one to four dot separated integers from 0 to 65535, a non zero part without a
    /// leading zero. Null when the text is not such a version.
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    public static int[] Parse(string version)
    {
        if (string.IsNullOrEmpty(version))
            return null;

        var parts = version.Split('.');

        if (parts.Length is < 1 or > 4)
            return null;

        var values = new int[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.Length is 0 or > 5 || !part.All(char.IsAsciiDigit) || (part.Length > 1 && part[0] == '0'))
                return null;

            var value = int.Parse(part, NumberStyles.None, CultureInfo.InvariantCulture);

            if (value > MaxPart)
                return null;

            values[i] = value;
        }

        return values;
    }

    /// <summary>
    /// Whether <paramref name="version"/> is a valid manifest version.
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    public static bool IsValid(string version) => Parse(version) != null;

    /// <summary>
    /// Compares two versions part by part (missing parts are zero); an invalid version sorts first.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static int Compare(string left, string right)
    {
        var a = Parse(left);
        var b = Parse(right);

        if (a == null || b == null)
            return (a == null ? 0 : 1) - (b == null ? 0 : 1);

        for (var i = 0; i < 4; i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;

            if (x != y)
                return x.CompareTo(y);
        }

        return 0;
    }
}
