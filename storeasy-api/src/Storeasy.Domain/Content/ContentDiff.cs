using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Domain.Content;

/// <summary>
/// Pure diff of the content model: desired values against the values the store has now, field by field. Only keys the
/// desired set names are compared (a plan is a partial update). Null and empty count as "no value".
/// </summary>
public static class ContentDiff
{
    /// <summary>
    /// Items for every desired key whose value differs from the current one: <see cref="ChangeAction.Create"/> when there
    /// is no current value, <see cref="ChangeAction.Delete"/> when the desired value is empty, otherwise
    /// <see cref="ChangeAction.Update"/>.
    /// </summary>
    /// <param name="current"></param>
    /// <param name="desired"></param>
    /// <returns></returns>
    public static List<PlanItem> Compute(IReadOnlyDictionary<string, string> current, IEnumerable<KeyValuePair<string, string>> desired)
    {
        var items = new List<PlanItem>();

        foreach (var (key, after) in desired)
        {
            var before = current.TryGetValue(key, out var value) ? value : null;

            if (Same(before, after))
                continue;

            var (group, locale, field) = ContentKey.Parse(key);

            items.Add(new PlanItem
            {
                Group = group,
                Locale = locale,
                Field = field,
                Before = Normalize(before),
                After = Normalize(after),
                Action = string.IsNullOrEmpty(before) ? ChangeAction.Create : string.IsNullOrEmpty(after) ? ChangeAction.Delete : ChangeAction.Update,
            });
        }

        return items;
    }

    /// <summary>
    /// Whether two values are the same (null and empty are equal; otherwise ordinal).
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool Same(string left, string right) => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);

    /// <summary>
    /// Lower case hex SHA-256 over the current values of <paramref name="keys"/> (sorted, so the order of the keys does not
    /// matter). A plan stores it; applying the plan reads the store again and refuses when it changed meanwhile.
    /// </summary>
    /// <param name="current"></param>
    /// <param name="keys"></param>
    /// <returns></returns>
    public static string Hash(IReadOnlyDictionary<string, string> current, IEnumerable<string> keys)
    {
        var builder = new StringBuilder();

        foreach (var key in keys.Distinct().Order(StringComparer.Ordinal))
            builder.Append(key).Append('=').Append(Normalize(current.TryGetValue(key, out var value) ? value : null)).Append('\n');

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    /// Number of UTF-8 bytes of <paramref name="value"/> (Apple counts keywords in bytes).
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static int ByteCount(string value) => string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);

    /// <summary>
    /// Number of characters as the stores count them (Unicode text elements, so an emoji counts once).
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static int CharCount(string value) => string.IsNullOrEmpty(value) ? 0 : new System.Globalization.StringInfo(value).LengthInTextElements;

    private static string Normalize(string value) => string.IsNullOrEmpty(value) ? null : value;
}
