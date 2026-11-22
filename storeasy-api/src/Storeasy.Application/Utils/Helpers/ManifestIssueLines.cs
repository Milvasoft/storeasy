using System.Globalization;
using System.Text.Json;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Shared pieces of the browser extension manifest readers (Chrome Web Store, Firefox Add-ons): the line an issue is kept
/// as in <c>ArtifactPackageInfo.ManifestIssues</c> and reading a string array from manifest.json.
/// </summary>
public static class ManifestIssueLines
{
    /// <summary>
    /// Separator of an issue line's arguments (unit separator, never in a manifest text).
    /// </summary>
    public const char ArgSeparator = '\u001f';

    /// <summary>
    /// The line an issue is kept as: <c>error|warning:key|English text|arg1␟arg2</c>.
    /// </summary>
    /// <param name="blocking"></param>
    /// <param name="key"></param>
    /// <param name="english"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static string Encode(bool blocking, string key, string english, IEnumerable<object> args)
        => $"{(blocking ? "error" : "warning")}:{key}|{english.Replace('|', '/')}|{string.Join(ArgSeparator, args.Select(a => Convert.ToString(a, CultureInfo.InvariantCulture)?.Replace('|', '/')))}";

    /// <summary>
    /// The string items of the array property <paramref name="name"/> of <paramref name="root"/>; empty when it is missing.
    /// </summary>
    /// <param name="root"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IEnumerable<string> Strings(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString())
            : [];
}
