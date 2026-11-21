using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Analytics;

/// <summary>
/// The Cloud Storage bucket of a Google Play developer account's reports. Play Console shows it as a URI under Download
/// reports (e.g. <c>gs://pubsite_prod_rev_01234567890987654321/</c>); the bucket name, the URI or the URI with a folder
/// are all accepted and stored as the bare bucket name.
/// </summary>
public static partial class ReportBuckets
{
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,220}[a-z0-9]$")]
    private static partial Regex BucketRegex();

    /// <summary>
    /// The bucket name of <paramref name="value"/>, empty for an empty value, or null when it is not a bucket name.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string Normalize(string value)
    {
        var text = value?.Trim() ?? string.Empty;

        if (text.Length == 0)
            return string.Empty;

        if (text.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
            text = text[5..];

        text = text.Split('/', 2)[0].ToLowerInvariant();

        return BucketRegex().IsMatch(text) ? text : null;
    }
}
