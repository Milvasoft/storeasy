using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// What storeasy reads from one workflow file.
/// </summary>
/// <param name="File">File name in <c>.github/workflows</c>.</param>
/// <param name="Registries">Registries the workflow publishes to.</param>
/// <param name="TagPatterns">Tag patterns of <c>on: push: tags</c>.</param>
/// <param name="Branches">Branches of <c>on: push: branches</c>.</param>
/// <param name="Paths">Path filters of <c>on: push: paths</c>.</param>
/// <param name="DispatchInputs">Inputs of <c>workflow_dispatch</c> (empty without inputs; null without dispatch).</param>
/// <param name="ProjectFiles">Project files the workflow names (<c>.csproj</c>).</param>
/// <param name="Text">The workflow text, for matching package names.</param>
public sealed record WorkflowFacts(string File, List<PackageRegistry> Registries, List<string> TagPatterns, List<string> Branches, List<string> Paths, List<string> DispatchInputs, List<string> ProjectFiles, string Text);

/// <summary>
/// A suggested release setting with why it was suggested.
/// </summary>
/// <param name="Settings">The settings.</param>
/// <param name="Reason">Plain explanation (workflow and trigger).</param>
public sealed record ReleaseSuggestion(ReleaseSettings Settings, string Reason);

/// <summary>
/// Reads a repository's workflows and suggests how storeasy starts a release (docs/package-registries.md): a tag push for
/// <c>on: push: tags</c>, a workflow dispatch for a <c>workflow_dispatch</c> with a version input, a version bump commit for
/// a push with path filters (the shared <c>Directory.Build.props</c> when the filters name it, else the project file the
/// workflow builds). The YAML is scanned, not executed; suggestions are for the member to check and save.
/// </summary>
public static partial class ReleaseDetection
{
    [GeneratedRegex(@"^(\s*)(tags|branches|paths)\s*:\s*(\[(?<inline>[^\]]*)\])?\s*$", RegexOptions.Multiline)]
    private static partial Regex ListKey();

    [GeneratedRegex(@"^(\s*)-\s*['""]?(?<item>[^'""#\s]+)['""]?\s*(#.*)?$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^(\s*)workflow_dispatch\s*:\s*$", RegexOptions.Multiline)]
    private static partial Regex DispatchKey();

    [GeneratedRegex(@"^\s*workflow_dispatch\s*:?\s*(\{\s*\}|null|~)?\s*$", RegexOptions.Multiline)]
    private static partial Regex DispatchAny();

    [GeneratedRegex(@"[A-Za-z0-9_./-]+\.csproj")]
    private static partial Regex ProjectFile();

    /// <summary>
    /// The facts of one workflow file.
    /// </summary>
    public static WorkflowFacts Analyze(string file, string text)
    {
        text = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);

        var registries = new List<PackageRegistry>();

        if (text.Contains("npm publish", StringComparison.Ordinal))
            registries.Add(PackageRegistry.Npm);

        if (text.Contains("gh-action-pypi-publish", StringComparison.Ordinal) || text.Contains("twine upload", StringComparison.Ordinal))
            registries.Add(PackageRegistry.PyPI);

        if (text.Contains("cargo publish", StringComparison.Ordinal))
            registries.Add(PackageRegistry.Crates);

        if (text.Contains("nuget push", StringComparison.OrdinalIgnoreCase) || text.Contains("NuGet/login", StringComparison.Ordinal) || text.Contains("publish-nuget", StringComparison.Ordinal) || text.Contains("nuget-publish", StringComparison.OrdinalIgnoreCase))
            registries.Add(PackageRegistry.NuGet);

        if (text.Contains("gem push", StringComparison.Ordinal) || text.Contains("release-gem", StringComparison.Ordinal))
            registries.Add(PackageRegistry.RubyGems);

        if (text.Contains("mvn ", StringComparison.Ordinal) && text.Contains("deploy", StringComparison.Ordinal))
            registries.Add(PackageRegistry.Maven);

        var lists = Lists(text);

        return new WorkflowFacts(file,
                                 registries,
                                 lists.GetValueOrDefault("tags", []),
                                 lists.GetValueOrDefault("branches", []),
                                 lists.GetValueOrDefault("paths", []),
                                 DispatchInputs(text),
                                 [.. ProjectFile().Matches(text).Select(m => m.Value.TrimStart('.', '/')).Distinct(StringComparer.Ordinal)],
                                 text);
    }

    /// <summary>
    /// Suggestions for <paramref name="packageName"/> on <paramref name="registry"/> from the workflows of
    /// <paramref name="repository"/>: the workflows that publish to the registry, those naming the package first.
    /// </summary>
    public static List<ReleaseSuggestion> Suggest(string repository, string branch, IEnumerable<WorkflowFacts> workflows, PackageRegistry registry, string packageName)
    {
        var shortName = packageName?.Split('/', ':').Last() ?? string.Empty;
        var candidates = workflows.Where(w => w.Registries.Contains(registry))
                                  .OrderByDescending(w => shortName.Length > 0 && (w.Text.Contains(shortName, StringComparison.OrdinalIgnoreCase) || w.ProjectFiles.Any(p => p.Contains(shortName, StringComparison.OrdinalIgnoreCase))))
                                  .ToList();
        var suggestions = new List<ReleaseSuggestion>();

        foreach (var workflow in candidates)
        {
            var settings = Base(repository, workflow.Branches.FirstOrDefault() ?? branch, workflow.File);

            if (workflow.TagPatterns.Count > 0)
            {
                var pattern = PatternOf(workflow.TagPatterns.FirstOrDefault(t => t.Contains(shortName, StringComparison.OrdinalIgnoreCase)) ?? workflow.TagPatterns[0]);

                suggestions.Add(new ReleaseSuggestion(With(settings, s => { s.Trigger = ReleaseTrigger.Tag; s.TagPattern = pattern; }),
                                                      $"{workflow.File} runs on tags {string.Join(", ", workflow.TagPatterns)}. If it checks the version in a manifest first, use a version bump and tag."));
            }

            if (workflow.DispatchInputs?.FirstOrDefault(i => i is "version" or "tag") is string input)
            {
                suggestions.Add(new ReleaseSuggestion(With(settings, s =>
                {
                    s.Trigger = ReleaseTrigger.Dispatch;
                    s.DispatchVersionInput = input;
                    s.DispatchTakesTag = input == "tag";
                    s.DispatchPrereleaseInput = workflow.DispatchInputs.Contains("prerelease") ? "prerelease" : null;
                }), $"{workflow.File} can be started with workflow_dispatch and the input {input}."));
            }

            if (workflow.TagPatterns.Count == 0 && workflow.Paths.Count > 0)
            {
                var file = workflow.Paths.Any(p => p.EndsWith("Directory.Build.props", StringComparison.OrdinalIgnoreCase))
                    ? "Directory.Build.props"
                    : workflow.ProjectFiles.FirstOrDefault(p => p.Contains(shortName, StringComparison.OrdinalIgnoreCase)) ?? workflow.ProjectFiles.FirstOrDefault();

                if (file != null)
                {
                    suggestions.Add(new ReleaseSuggestion(With(settings, s =>
                    {
                        s.Trigger = ReleaseTrigger.VersionBump;
                        s.VersionFiles = [new VersionFileSetting { Path = file, Format = FormatOf(file) }];
                    }), $"{workflow.File} runs on a push to {settings.Branch} touching {string.Join(", ", workflow.Paths)}; the version is in {file}."));
                }
            }
        }

        return suggestions;
    }

    /// <summary>
    /// The version file format of a path.
    /// </summary>
    public static VersionFileFormat FormatOf(string path)
    {
        var name = path.Split('/').Last();

        if (name.EndsWith(".props", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
            return VersionFileFormat.MsBuild;

        if (name.Equals("package.json", StringComparison.OrdinalIgnoreCase))
            return VersionFileFormat.PackageJson;

        if (name.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
            return VersionFileFormat.Toml;

        if (name.EndsWith(".gemspec", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rb", StringComparison.OrdinalIgnoreCase))
            return VersionFileFormat.Ruby;

        return name.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ? VersionFileFormat.Pom : VersionFileFormat.PlainText;
    }

    /// <summary>
    /// A tag pattern of storeasy for a workflow's glob (<c>sdk-rust-v*</c> and <c>v*.*.*</c> give
    /// <c>sdk-rust-v{version}</c> and <c>v{version}</c>).
    /// </summary>
    public static string PatternOf(string glob)
    {
        var star = glob.IndexOf('*', StringComparison.Ordinal);

        return star < 0 ? glob : glob[..star] + ReleaseRules.VersionPlaceholder;
    }

    private static Dictionary<string, List<string>> Lists(string text)
    {
        var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var key = ListKey().Match(lines[i]);

            if (!key.Success)
                continue;

            var name = key.Groups[2].Value;
            var items = lists.TryGetValue(name, out var existing) ? existing : lists[name] = [];

            if (key.Groups["inline"].Success)
            {
                items.AddRange(key.Groups["inline"].Value.Split(',').Select(v => v.Trim().Trim('\'', '"')).Where(v => v.Length > 0));
                continue;
            }

            for (var j = i + 1; j < lines.Length && ListItem().Match(lines[j]) is { Success: true } item; j++)
                items.Add(item.Groups["item"].Value);
        }

        foreach (var list in lists.Values)
        {
            var distinct = list.Distinct(StringComparer.Ordinal).ToList();

            list.Clear();
            list.AddRange(distinct);
        }

        return lists;
    }

    private static List<string> DispatchInputs(string text)
    {
        var key = DispatchKey().Match(text);

        if (!key.Success)
            return DispatchAny().IsMatch(text) ? [] : null;

        var indent = key.Groups[1].Value.Length;
        var lines = text[(key.Index + key.Length)..].Split('\n').Skip(1).ToList();
        var inputs = new List<string>();
        int? inputIndent = null;

        foreach (var line in lines)
        {
            if (line.Trim().Length == 0)
                continue;

            var depth = line.Length - line.TrimStart().Length;

            if (depth <= indent)
                break;

            var trimmed = line.Trim();

            if (trimmed == "inputs:")
            {
                inputIndent = null;
                continue;
            }

            if (trimmed.EndsWith(':') && !trimmed.Contains(' ', StringComparison.Ordinal))
            {
                inputIndent ??= depth;

                if (depth == inputIndent)
                    inputs.Add(trimmed.TrimEnd(':'));
            }
        }

        return inputs;
    }

    private static ReleaseSettings Base(string repository, string branch, string workflow) => new() { Repository = repository, Branch = branch, Workflow = workflow };

    private static ReleaseSettings With(ReleaseSettings source, Action<ReleaseSettings> change)
    {
        var copy = new ReleaseSettings { Repository = source.Repository, Branch = source.Branch, Workflow = source.Workflow };

        change(copy);

        return copy;
    }
}
