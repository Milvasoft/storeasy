using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// A suggested group: tracked packages that come from one source repository.
/// </summary>
/// <param name="Name">Suggested name (the repository path).</param>
/// <param name="Repository">The shared repository.</param>
/// <param name="TrackedPackageIds">Members.</param>
public sealed record GroupSuggestion(string Name, string Repository, List<int> TrackedPackageIds);

/// <summary>
/// Package groups (docs/package-registries.md): version order, which members are behind the group's highest version, and
/// suggestions by shared source repository.
/// </summary>
public static class PackageGroups
{
    /// <summary>
    /// Smallest group suggested.
    /// </summary>
    public const int MinSuggestedMembers = 2;

    /// <summary>
    /// Compares two version strings: dot separated numbers numerically (missing parts are 0), a prerelease (<c>-</c>
    /// suffix) below its release, then the prerelease text; build metadata (<c>+</c>) is ignored.
    /// </summary>
    public static int Compare(string left, string right)
    {
        if (left == right)
            return 0;

        if (left == null)
            return -1;

        if (right == null)
            return 1;

        var (leftCore, leftPre) = Split(left);
        var (rightCore, rightPre) = Split(right);
        var length = Math.Max(leftCore.Length, rightCore.Length);

        for (var i = 0; i < length; i++)
        {
            var a = i < leftCore.Length ? leftCore[i] : "0";
            var b = i < rightCore.Length ? rightCore[i] : "0";
            var bothNumbers = long.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var x) & long.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var y);
            var order = bothNumbers ? x.CompareTo(y) : string.CompareOrdinal(a, b);

            if (order != 0)
                return order;
        }

        if (leftPre == rightPre)
            return 0;

        if (leftPre == null)
            return 1;

        return rightPre == null ? -1 : string.CompareOrdinal(leftPre, rightPre);
    }

    /// <summary>
    /// The highest of <paramref name="versions"/>.
    /// </summary>
    public static string Highest(IEnumerable<string> versions)
        => versions.Where(v => !string.IsNullOrEmpty(v)).Aggregate((string)null, (best, v) => best == null || Compare(v, best) > 0 ? v : best);

    /// <summary>
    /// Groups of at least <see cref="MinSuggestedMembers"/> ungrouped packages sharing a source repository, largest first.
    /// </summary>
    public static List<GroupSuggestion> Suggest(IEnumerable<TrackedPackage> packages)
        => [.. packages.Where(p => p.PackageGroupId == null && !string.IsNullOrWhiteSpace(p.SourceRepository))
                       .GroupBy(p => RepositoryKey(p.SourceRepository), StringComparer.OrdinalIgnoreCase)
                       .Where(g => g.Count() >= MinSuggestedMembers)
                       .Select(g => new GroupSuggestion(g.Key, g.First().SourceRepository, [.. g.Select(p => p.Id).Order()]))
                       .OrderByDescending(s => s.TrackedPackageIds.Count)
                       .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The <c>owner/repo</c> of a repository address (host, scheme, <c>.git</c>, trailing slash and sub folders such as
    /// <c>/tree/main/x</c> dropped).
    /// </summary>
    public static string RepositoryKey(string repository)
    {
        var value = repository.Trim();
        var scheme = value.IndexOf("://", StringComparison.Ordinal);

        value = scheme >= 0 ? value[(scheme + 3)..] : value;

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var path = parts.Length >= 3 ? $"{parts[1]}/{parts[2]}" : string.Join('/', parts);

        return path.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;
    }

    private static (string[] Core, string Pre) Split(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        var value = plus >= 0 ? version[..plus] : version;
        var dash = value.IndexOf('-', StringComparison.Ordinal);

        return dash >= 0 ? (value[..dash].Split('.'), value[(dash + 1)..]) : (value.Split('.'), null);
    }
}
