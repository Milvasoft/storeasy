using System.Globalization;

namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// winget's version order (winget-pkgs doc/Authoring.md, "Version sorting"): versions split on dots, each part compared by
/// its leading number then by the text after it, where a part without text is higher than the same number with text
/// (1.2.34 &gt; 1.2.34-beta); missing parts count as 0 (1.2 = 1.2.0). Leading non digits are trimmed, as newer clients do.
/// </summary>
public sealed class WingetVersions : IComparer<string>
{
    /// <summary>
    /// The shared comparer.
    /// </summary>
    public static WingetVersions Comparer { get; } = new();

    /// <summary>
    /// Versions highest first.
    /// </summary>
    /// <param name="versions"></param>
    /// <returns></returns>
    public static List<string> Descending(IEnumerable<string> versions) => [.. versions.OrderByDescending(v => v, Comparer)];

    /// <inheritdoc/>
    public int Compare(string x, string y)
    {
        var left = Parts(x);
        var right = Parts(y);
        var count = Math.Max(left.Count, right.Count);

        for (var i = 0; i < count; i++)
        {
            var a = i < left.Count ? left[i] : (0, string.Empty);
            var b = i < right.Count ? right[i] : (0, string.Empty);

            var byNumber = a.Number.CompareTo(b.Number);

            if (byNumber != 0)
                return byNumber;

            if (a.Text.Length == 0 && b.Text.Length > 0)
                return 1;

            if (a.Text.Length > 0 && b.Text.Length == 0)
                return -1;

            var byText = string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);

            if (byText != 0)
                return byText;
        }

        return 0;
    }

    private static List<(ulong Number, string Text)> Parts(string version)
    {
        var text = (version ?? string.Empty).Trim();
        var start = 0;

        while (start < text.Length && !char.IsAsciiDigit(text[start]))
            start++;

        text = text[start..];

        var parts = text.Split('.').Select(part =>
        {
            var digits = 0;

            while (digits < part.Length && char.IsAsciiDigit(part[digits]))
                digits++;

            var number = digits == 0 ? 0UL : ulong.TryParse(part.AsSpan(0, Math.Min(digits, 19)), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : ulong.MaxValue;

            return (number, part[digits..].Trim());
        }).ToList();

        // Trailing zero parts do not change the order (1.2 = 1.2.0).
        while (parts.Count > 1 && parts[^1] is (0, ""))
            parts.RemoveAt(parts.Count - 1);

        return parts;
    }
}
