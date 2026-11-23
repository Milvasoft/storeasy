using System.Globalization;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// A version a release can take.
/// </summary>
/// <param name="Kind"><c>patch</c>, <c>minor</c>, <c>major</c> or <c>prerelease</c>.</param>
/// <param name="Version">The version.</param>
public sealed record VersionSuggestion(string Kind, string Version);

/// <summary>
/// Where the version is in a version file.
/// </summary>
/// <param name="Index">Start of the version in the text.</param>
/// <param name="Version">The version.</param>
/// <param name="Line">Line number (1 based).</param>
public sealed record VersionLocation(int Index, string Version, int Line);

/// <summary>
/// Releases started in CI (docs/package-registries.md): next versions, version checks per registry, finding and replacing
/// the version in version files, tag names and release settings checks.
/// </summary>
public static partial class ReleaseRules
{
    /// <summary>
    /// The placeholder of a tag pattern.
    /// </summary>
    public const string VersionPlaceholder = "{version}";

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$")]
    private static partial Regex SemVer();

    // NuGet: 2 to 4 numeric parts and an optional SemVer 2.0.0 prerelease and build.
    [GeneratedRegex(@"^\d+(\.\d+){1,3}(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$")]
    private static partial Regex NuGetVersion();

    // PyPI: a public PEP 440 version (release, optional a/b/rc pre, .post, .dev).
    [GeneratedRegex(@"^\d+(\.\d+)*((a|b|rc)\d+)?(\.post\d+)?(\.dev\d+)?$")]
    private static partial Regex Pep440();

    // RubyGems, Maven, Packagist: dot separated parts, letters allowed after the first number.
    [GeneratedRegex(@"^\d+(\.[0-9A-Za-z]+)*([-.][0-9A-Za-z.]+)?$")]
    private static partial Regex LooseVersion();

    [GeneratedRegex(@"<PackageVersion>\s*([^<\s]+)\s*</PackageVersion>")]
    private static partial Regex MsBuildPackageVersion();

    [GeneratedRegex(@"<Version>\s*([^<\s]+)\s*</Version>")]
    private static partial Regex MsBuildVersion();

    [GeneratedRegex("\"version\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex PackageJsonVersion();

    [GeneratedRegex("^\\s*version\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.Multiline)]
    private static partial Regex TomlVersion();

    [GeneratedRegex("(?:\\.version|VERSION)\\s*=\\s*[\"']([^\"']+)[\"']")]
    private static partial Regex RubyVersion();

    [GeneratedRegex(@"<parent>.*?</parent>", RegexOptions.Singleline)]
    private static partial Regex PomParent();

    [GeneratedRegex(@"<version>\s*([^<\s]+)\s*</version>")]
    private static partial Regex PomVersion();

    [GeneratedRegex(@"^[A-Za-z0-9._/-]+$")]
    private static partial Regex RefName();

    [GeneratedRegex(@"^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$")]
    private static partial Regex GitHubRepository();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+(/[A-Za-z0-9._ -]+)*$")]
    private static partial Regex FilePath();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+\.ya?ml$")]
    private static partial Regex WorkflowFile();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_-]*$")]
    private static partial Regex InputName();

    /// <summary>
    /// The next patch, minor and major versions of <paramref name="current"/> and, when <paramref name="prerelease"/> is
    /// given, the next patch as that prerelease (for example <c>rc.1</c>). Only the numeric core is kept: <c>11.0.2</c> gives
    /// <c>11.0.3</c>, <c>11.1.0</c>, <c>12.0.0</c>.
    /// </summary>
    public static List<VersionSuggestion> Next(string current, string prerelease = null)
    {
        var parts = Core(current);
        var major = parts.ElementAtOrDefault(0);
        var minor = parts.ElementAtOrDefault(1);
        var patch = parts.ElementAtOrDefault(2);
        var stable = string.IsNullOrEmpty(current) || !current.Contains('-', StringComparison.Ordinal);
        var nextPatch = $"{major}.{minor}.{(stable ? patch + 1 : patch)}";
        var list = new List<VersionSuggestion>
        {
            new("patch", nextPatch),
            new("minor", $"{major}.{minor + 1}.0"),
            new("major", $"{major + 1}.0.0"),
        };

        if (!string.IsNullOrWhiteSpace(prerelease))
            list.Add(new VersionSuggestion("prerelease", $"{nextPatch}-{prerelease.Trim()}"));

        return list;
    }

    /// <summary>
    /// Whether <paramref name="version"/> is a version <paramref name="registry"/> accepts: SemVer 2.0.0 on npm,
    /// crates.io and pub.dev; NuGet's 2 to 4 part versions; PEP 440 on PyPI; dot separated versions elsewhere.
    /// </summary>
    public static bool IsValidVersion(PackageRegistry registry, string version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64)
            return false;

        return registry switch
        {
            PackageRegistry.Npm or PackageRegistry.Crates or PackageRegistry.Pub => SemVer().IsMatch(version),
            PackageRegistry.NuGet => NuGetVersion().IsMatch(version),
            PackageRegistry.PyPI => Pep440().IsMatch(version),
            _ => LooseVersion().IsMatch(version),
        };
    }

    /// <summary>
    /// The tag of <paramref name="version"/> for <paramref name="pattern"/> (<c>v{version}</c> gives <c>v1.2.3</c>).
    /// </summary>
    public static string TagOf(string pattern, string version) => pattern?.Replace(VersionPlaceholder, version, StringComparison.Ordinal);

    /// <summary>
    /// Where the version is in <paramref name="content"/>; null when the file has none.
    /// </summary>
    public static VersionLocation Locate(string content, VersionFileFormat format)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        Match match;

        switch (format)
        {
            case VersionFileFormat.MsBuild:
                match = MsBuildPackageVersion().Match(content);
                match = match.Success ? match : MsBuildVersion().Match(content);
                break;
            case VersionFileFormat.PackageJson:
                match = PackageJsonVersion().Match(content);
                break;
            case VersionFileFormat.Toml:
                match = TomlVersion().Match(content);
                break;
            case VersionFileFormat.Ruby:
                match = RubyVersion().Match(content);
                break;
            case VersionFileFormat.Pom:
                return LocatePom(content);
            default:
                var trimmed = content.Trim();
                return trimmed.Length == 0 || trimmed.Contains('\n', StringComparison.Ordinal) ? null : new VersionLocation(content.IndexOf(trimmed, StringComparison.Ordinal), trimmed, 1);
        }

        return match.Success ? new VersionLocation(match.Groups[1].Index, match.Groups[1].Value, LineOf(content, match.Groups[1].Index)) : null;
    }

    /// <summary>
    /// <paramref name="content"/> with the version at <paramref name="location"/> replaced by <paramref name="version"/>.
    /// </summary>
    public static string Replace(string content, VersionLocation location, string version)
        => string.Concat(content.AsSpan(0, location.Index), version, content.AsSpan(location.Index + location.Version.Length));

    /// <summary>
    /// The line of <paramref name="content"/> holding <paramref name="index"/>.
    /// </summary>
    public static string LineAt(string content, int index)
    {
        var start = content.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var end = content.IndexOf('\n', index);

        return content[start..(end < 0 ? content.Length : end)].TrimEnd('\r');
    }

    /// <summary>
    /// Problems of release settings (message keys), empty when they can start a release.
    /// </summary>
    public static List<string> Problems(ReleaseSettings settings)
    {
        var problems = new List<string>();

        if (settings == null)
            return [MessageKey.RegistryReleaseNotSet];

        if (!GitHubRepository().IsMatch(settings.Repository ?? string.Empty))
            problems.Add(MessageKey.RegistrySetupRepositoryInvalid);

        if (string.IsNullOrWhiteSpace(settings.Branch) || !RefName().IsMatch(settings.Branch) || settings.Branch.Contains("..", StringComparison.Ordinal))
            problems.Add(MessageKey.RegistryReleaseBranchInvalid);

        var bumps = settings.Trigger is ReleaseTrigger.VersionBump or ReleaseTrigger.VersionBumpAndTag;
        var tags = settings.Trigger is ReleaseTrigger.Tag or ReleaseTrigger.VersionBumpAndTag;

        if (bumps && (settings.VersionFiles.Count == 0 || settings.VersionFiles.Any(f => !FilePath().IsMatch(f.Path ?? string.Empty) || f.Path.Contains("..", StringComparison.Ordinal) || !Enum.IsDefined(f.Format))))
            problems.Add(MessageKey.RegistryReleaseVersionFilesInvalid);

        if (tags && (string.IsNullOrWhiteSpace(settings.TagPattern) || !settings.TagPattern.Contains(VersionPlaceholder, StringComparison.Ordinal) || !RefName().IsMatch(TagOf(settings.TagPattern, "1.0.0"))))
            problems.Add(MessageKey.RegistryReleaseTagPatternInvalid);

        if (settings.Trigger == ReleaseTrigger.Dispatch && (!InputName().IsMatch(settings.DispatchVersionInput ?? string.Empty) || (settings.DispatchPrereleaseInput != null && !InputName().IsMatch(settings.DispatchPrereleaseInput))))
            problems.Add(MessageKey.RegistryReleaseDispatchInvalid);

        if ((settings.Trigger == ReleaseTrigger.Dispatch || !string.IsNullOrEmpty(settings.Workflow)) && !WorkflowFile().IsMatch(settings.Workflow ?? string.Empty))
            problems.Add(MessageKey.RegistrySetupWorkflowInvalid);

        return problems;
    }

    /// <summary>
    /// The inputs of a workflow dispatch for <paramref name="version"/>.
    /// </summary>
    public static Dictionary<string, string> DispatchInputs(ReleaseSettings settings, string version, string tag)
    {
        var inputs = new Dictionary<string, string> { [settings.DispatchVersionInput] = settings.DispatchTakesTag ? tag ?? $"v{version}" : version };

        if (!string.IsNullOrEmpty(settings.DispatchPrereleaseInput))
            inputs[settings.DispatchPrereleaseInput] = version.Contains('-', StringComparison.Ordinal) ? "true" : "false";

        return inputs;
    }

    private static VersionLocation LocatePom(string content)
    {
        var parent = PomParent().Match(content);

        foreach (Match match in PomVersion().Matches(content))
        {
            if (parent.Success && match.Index >= parent.Index && match.Index < parent.Index + parent.Length)
                continue;

            return new VersionLocation(match.Groups[1].Index, match.Groups[1].Value, LineOf(content, match.Groups[1].Index));
        }

        return null;
    }

    private static int LineOf(string content, int index) => content.AsSpan(0, index).Count('\n') + 1;

    private static int[] Core(string version)
    {
        var core = (version ?? "0.0.0").Split('-', '+')[0];

        return [.. core.Split('.').Select(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).Concat([0, 0, 0]).Take(3)];
    }
}
