using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Content;
using System.Globalization;
using System.Text;

namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// What a winget plan is built from.
/// </summary>
/// <param name="PackageIdentifier">Package identifier.</param>
/// <param name="Base">The package as read now (the latest version for a new version, the edited version for texts).</param>
/// <param name="Kind">New version or locale texts.</param>
/// <param name="Version">New version, or the version whose texts change (null: the latest).</param>
/// <param name="Installers">Installers of a new version.</param>
/// <param name="Locales">Locale text edits.</param>
/// <param name="OpenPullRequests">Open pull requests storeasy knows for the package: version to number.</param>
/// <param name="Now">Current UTC instant (branch names).</param>
public sealed record WingetPlanInput(string PackageIdentifier,
                                     StoreContentSnapshot Base,
                                     WingetChangeKind Kind,
                                     string Version,
                                     IReadOnlyList<WingetInstallerInputDto> Installers,
                                     IReadOnlyList<WingetLocaleInputDto> Locales,
                                     IReadOnlyDictionary<string, int> OpenPullRequests,
                                     DateTime Now);

/// <summary>
/// A built winget plan, or the issues that stop it.
/// </summary>
/// <param name="Draft">The plan; null when blocked.</param>
/// <param name="Issues">Every issue found (blocking and not).</param>
public sealed record WingetPlanResult(PlanDraft Draft, List<WingetIssue> Issues)
{
    /// <summary>
    /// Whether a blocking issue stops the plan.
    /// </summary>
    public bool IsBlocked => this.Draft == null;
}

/// <summary>
/// Builds the manifests of a winget pull request from the package's current manifests and the requested change, the way
/// wingetcreate's update does: the latest version's manifests are copied, the version and the installers are replaced,
/// version specific fields (release notes, release date, product codes, hashes) are dropped unless given again, and the
/// result is checked with <see cref="WingetManifestValidator"/>.
/// </summary>
public static class WingetPlanner
{
    /// <summary>
    /// Plan item group of locale texts.
    /// </summary>
    public const string ListingGroup = ContentGroups.Listing;

    /// <summary>
    /// Plan item group of installer entries.
    /// </summary>
    public const string InstallerGroup = "Installer";

    /// <summary>
    /// Plan item group of manifest files.
    /// </summary>
    public const string ManifestGroup = "Manifest";

    private static readonly string[] _versionSpecificRoot = ["ReleaseDate", "ProductCode", "AppsAndFeaturesEntries"];

    private static readonly string[] _versionSpecificInstaller = ["InstallerUrl", "InstallerSha256", "SignatureSha256", "ProductCode", "AppsAndFeaturesEntries", "ReleaseDate"];

    /// <summary>
    /// Builds the plan.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static WingetPlanResult Build(WingetPlanInput input)
    {
        var issues = new List<WingetIssue>();
        var baseFiles = WingetContent.FilesOf(input.Base);
        var versions = WingetContent.VersionsOf(input.Base);
        var current = baseFiles.Count > 0 ? WingetManifestSet.FromFiles(baseFiles) : null;
        var version = (input.Version ?? (input.Kind == WingetChangeKind.Listing ? input.Base.VersionId : null))?.Trim();

        if (string.IsNullOrEmpty(version))
            return Blocked(issues, new(MessageKey.WingetFieldRequired, ["PackageVersion", WingetManifestSet.VersionType], "PackageVersion"));

        if (input.OpenPullRequests.TryGetValue(version, out var open))
            issues.Add(new(MessageKey.WingetVersionPullRequestOpen, [version, open], "PackageVersion"));

        WingetManifestSet next;

        if (input.Kind == WingetChangeKind.NewVersion)
        {
            if (versions.Contains(version, StringComparer.OrdinalIgnoreCase))
                issues.Add(new(MessageKey.WingetVersionExists, [version], "PackageVersion"));

            if (input.Installers.Count == 0)
                issues.Add(new(MessageKey.WingetInstallersRequired, [], "Installers"));

            next = current?.Clone() ?? NewSet(input);
            SetVersion(next, input.PackageIdentifier, version);
            DropVersionSpecific(next);
            ReplaceInstallers(next, current, input.Installers, issues);
        }
        else
        {
            if (current == null || !string.Equals(current.PackageVersion, version, StringComparison.OrdinalIgnoreCase))
                return Blocked(issues, new(MessageKey.WingetVersionMissing, [version], "PackageVersion"));

            next = current.Clone();
        }

        ApplyLocales(next, input.Locales, issues);
        issues.AddRange(WingetManifestValidator.Validate(next));

        if (issues.Any(i => i.Blocking))
            return new(null, issues);

        var draft = Draft(input, current, next, version, baseFiles);

        if (input.Kind == WingetChangeKind.Listing && draft.Items.All(i => i.Group == ManifestGroup))
            return Blocked(issues, new(MessageKey.WingetNoChanges, [], null));

        return new(draft, issues);
    }

    private static WingetPlanResult Blocked(List<WingetIssue> issues, WingetIssue issue)
    {
        issues.Add(issue);

        return new(null, issues);
    }

    private static WingetManifestSet NewSet(WingetPlanInput input)
    {
        var locale = input.Locales.FirstOrDefault()?.Locale?.Trim() ?? "en-US";
        var set = new WingetManifestSet();

        set.Version.Set("PackageIdentifier", input.PackageIdentifier);
        set.Version.Set("PackageVersion", string.Empty);
        set.Version.Set("DefaultLocale", locale);
        set.Version.Set("ManifestType", WingetManifestSet.VersionType);
        set.Version.Set("ManifestVersion", WingetFields.DefaultManifestVersion);

        set.Installer.Set("PackageIdentifier", input.PackageIdentifier);
        set.Installer.Set("PackageVersion", string.Empty);
        set.Installer.Set("Installers", new List<object>());
        set.Installer.Set("ManifestType", WingetManifestSet.InstallerType);
        set.Installer.Set("ManifestVersion", WingetFields.DefaultManifestVersion);

        set.DefaultLocale.Set("PackageIdentifier", input.PackageIdentifier);
        set.DefaultLocale.Set("PackageVersion", string.Empty);
        set.DefaultLocale.Set("PackageLocale", locale);
        set.DefaultLocale.Set("ManifestType", WingetManifestSet.DefaultLocaleType);
        set.DefaultLocale.Set("ManifestVersion", WingetFields.DefaultManifestVersion);

        return set;
    }

    private static IEnumerable<YamlMap> Manifests(WingetManifestSet set) => new[] { set.Version, set.Installer, set.DefaultLocale }.Concat(set.Locales.Values);

    private static void SetVersion(WingetManifestSet set, string packageIdentifier, string version)
    {
        foreach (var manifest in Manifests(set))
        {
            manifest.Set("PackageIdentifier", packageIdentifier);
            manifest.Set("PackageVersion", version);
            manifest.Set("ManifestVersion", WingetFields.DefaultManifestVersion);
        }
    }

    private static void DropVersionSpecific(WingetManifestSet set)
    {
        foreach (var key in _versionSpecificRoot)
            set.Installer.Set(key, null);

        // Release notes describe one version; a new version gets them only when the plan gives them.
        foreach (var locale in set.LocaleTags.ToList())
        {
            set.Locale(locale).Set("ReleaseNotes", null);
            set.Locale(locale).Set("ReleaseNotesUrl", null);
        }
    }

    private static void ReplaceInstallers(WingetManifestSet next, WingetManifestSet current, IReadOnlyList<WingetInstallerInputDto> inputs, List<WingetIssue> issues)
    {
        var previous = (current?.Installer.Seq("Installers") ?? []).OfType<YamlMap>().ToList();
        var rootType = next.Installer.Text("InstallerType");
        var list = new List<object>();

        foreach (var input in inputs)
        {
            var architecture = input.Architecture?.Trim().ToLowerInvariant();
            var type = string.IsNullOrWhiteSpace(input.Type) ? null : input.Type.Trim().ToLowerInvariant();
            var scope = string.IsNullOrWhiteSpace(input.Scope) ? null : input.Scope.Trim().ToLowerInvariant();

            // The closest previous entry keeps what does not change between versions (switches, upgrade behavior,
            // dependencies, commands); its version specific fields are dropped.
            var template = previous.FirstOrDefault(p => p.Text("Architecture") == architecture && p.Text("Scope") == scope && (type == null || (p.Text("InstallerType") ?? rootType) == type))
                           ?? previous.FirstOrDefault(p => p.Text("Architecture") == architecture)
                           ?? previous.FirstOrDefault();
            var entry = template?.Clone() ?? [];

            foreach (var key in _versionSpecificInstaller)
                entry.Set(key, null);

            entry.Set("Architecture", architecture);
            entry.Set("InstallerType", type == null || type == rootType ? (rootType == null ? entry.Text("InstallerType") : null) : type);
            entry.Set("Scope", scope);
            entry.Set("InstallerLocale", string.IsNullOrWhiteSpace(input.Locale) ? entry.Text("InstallerLocale") : input.Locale.Trim());
            entry.Set("InstallerUrl", input.Url?.Trim());
            entry.Set("InstallerSha256", string.IsNullOrWhiteSpace(input.Sha256) ? WingetManifestValidator.PendingSha256 : input.Sha256.Trim().ToUpperInvariant());

            if (!string.IsNullOrWhiteSpace(input.SilentSwitches) || !string.IsNullOrWhiteSpace(input.SilentWithProgressSwitches))
            {
                var switches = entry.Map("InstallerSwitches")?.Clone() ?? [];

                if (!string.IsNullOrWhiteSpace(input.SilentSwitches))
                    switches.Set("Silent", input.SilentSwitches.Trim());

                if (!string.IsNullOrWhiteSpace(input.SilentWithProgressSwitches))
                    switches.Set("SilentWithProgress", input.SilentWithProgressSwitches.Trim());

                entry.Set("InstallerSwitches", switches);
            }

            entry.Set("ProductCode", string.IsNullOrWhiteSpace(input.ProductCode) ? null : input.ProductCode.Trim());

            list.Add(Ordered(entry));
        }

        next.Installer.Set("Installers", list, before: "ManifestType");

        if (list.Count > 0 && next.Installer.Text("InstallerType") == null && list.OfType<YamlMap>().Any(i => i.Text("InstallerType") == null))
            issues.Add(new(MessageKey.WingetFieldRequired, ["InstallerType", WingetManifestSet.InstallerType], "InstallerType"));
    }

    // Architecture first, then the address and hash, as winget tools write them.
    private static YamlMap Ordered(YamlMap entry)
    {
        var order = new[] { "Architecture", "InstallerType", "Scope", "InstallerLocale", "InstallerUrl", "InstallerSha256", "InstallerSwitches", "ProductCode" };
        var ordered = new YamlMap();

        foreach (var key in order)
        {
            if (entry.Get(key) is { } value)
                ordered.Add(new(key, value));
        }

        ordered.AddRange(entry.Where(p => !order.Contains(p.Key)));

        return ordered;
    }

    private static void ApplyLocales(WingetManifestSet set, IReadOnlyList<WingetLocaleInputDto> locales, List<WingetIssue> issues)
    {
        foreach (var input in locales)
        {
            var locale = input.Locale?.Trim();

            if (string.IsNullOrEmpty(locale) || !WingetFields.Locale().IsMatch(locale))
            {
                issues.Add(new(MessageKey.WingetFieldInvalid, ["PackageLocale", locale ?? "-"], "PackageLocale"));
                continue;
            }

            var manifest = set.Locale(locale);

            if (manifest == null)
            {
                manifest = [];
                manifest.Set("PackageIdentifier", set.PackageIdentifier);
                manifest.Set("PackageVersion", set.PackageVersion);
                manifest.Set("PackageLocale", locale);
                manifest.Set("ManifestType", WingetManifestSet.LocaleType);
                manifest.Set("ManifestVersion", set.ManifestVersion);
                set.Locales[locale] = manifest;
            }

            var isDefault = ReferenceEquals(manifest, set.DefaultLocale);

            foreach (var (name, value) in input.Fields ?? [])
            {
                var field = WingetFields.ListingFields.FirstOrDefault(f => f.Name == name);

                if (field == null || (field.DefaultOnly && !isDefault))
                {
                    issues.Add(new(MessageKey.WingetFieldNotEditable, [name], name, locale));
                    continue;
                }

                manifest.Set(name, string.IsNullOrWhiteSpace(value) ? null : value.Replace("\r\n", "\n").Trim(), before: "ManifestType");
            }

            if (input.Tags != null)
            {
                var tags = input.Tags.Select(t => t?.Trim().ToLowerInvariant()).Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.Ordinal).Cast<object>().ToList();

                manifest.Set("Tags", tags.Count == 0 ? null : tags, before: "ManifestType");
            }
        }
    }

    private static PlanDraft Draft(WingetPlanInput input, WingetManifestSet current, WingetManifestSet next, string version, Dictionary<string, string> baseFiles)
    {
        var folder = WingetPaths.VersionFolder(input.PackageIdentifier, version);

        // A text edit pushes only the manifests it changes; the others keep their text in winget-pkgs.
        var files = next.ToFiles()
                        .Where(f => input.Kind != WingetChangeKind.Listing || current?.WasSingleton == true || !Unchanged(baseFiles.GetValueOrDefault(f.Path[(folder.Length + 1)..]), f.Content))
                        .ToList();
        var deletes = new List<string>();

        // A singleton manifest of the edited version is replaced by the multi-file one.
        if (input.Kind == WingetChangeKind.Listing && current?.WasSingleton == true)
            deletes.AddRange(baseFiles.Keys.Select(name => $"{folder}/{name}").Where(path => files.All(f => f.Path != path)));

        var items = new List<PlanItem>();

        foreach (var locale in next.LocaleTags)
        {
            var before = current?.Locale(locale);
            var after = next.Locale(locale);

            foreach (var name in WingetFields.ListingFields.Select(f => f.Name).Append("Tags"))
            {
                var old = TextOf(before, name);
                var value = TextOf(after, name);

                if (old == value)
                    continue;

                items.Add(new PlanItem
                {
                    Group = ListingGroup,
                    Locale = locale,
                    Field = name,
                    Before = old,
                    After = value,
                    Action = old == null ? ChangeAction.Create : value == null ? ChangeAction.Delete : ChangeAction.Update,
                });
            }
        }

        var installers = WingetContent.InstallersOf(next);

        if (input.Kind == WingetChangeKind.NewVersion)
        {
            items.AddRange(installers.Select(i => new PlanItem
            {
                Group = InstallerGroup,
                Field = string.Join(' ', new[] { i.Architecture, i.Scope, i.Type }.Where(v => v != null)),
                After = i.Url,
                Action = ChangeAction.Create,
            }));
        }

        foreach (var file in files)
        {
            var name = file.Path[(folder.Length + 1)..];
            var old = baseFiles.GetValueOrDefault(name);

            if (input.Kind == WingetChangeKind.NewVersion)
                old = null;

            if (old != null && Normalize(old) == Normalize(file.Content))
                continue;

            items.Add(new PlanItem { Group = ManifestGroup, Field = file.Path, Before = old, After = file.Content, Action = old == null ? ChangeAction.Create : ChangeAction.Update });
        }

        items.AddRange(deletes.Select(path => new PlanItem { Group = ManifestGroup, Field = path, Before = baseFiles.GetValueOrDefault(path[(folder.Length + 1)..]), Action = ChangeAction.Delete }));

        var newPackage = current == null;
        var title = input.Kind == WingetChangeKind.Listing
            ? $"Update metadata: {input.PackageIdentifier} version {version}"
            : newPackage
                ? $"New package: {input.PackageIdentifier} version {version}"
                : $"New version: {input.PackageIdentifier} version {version}";

        var branch = $"storeasy/{input.PackageIdentifier}-{version}-{input.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
        var change = new WingetPlannedChange
        {
            Kind = input.Kind,
            PackageIdentifier = input.PackageIdentifier,
            PackageVersion = version,
            BaseVersion = current?.PackageVersion,
            Files = files,
            DeletePaths = deletes,
            Installers = input.Kind != WingetChangeKind.NewVersion ? [] : [.. installers.Select((i, index) => new WingetPlannedInstaller(index, i.Url, i.Sha256 == WingetManifestValidator.PendingSha256 ? null : i.Sha256))],
            Branch = SafeBranch(branch),
            Title = title,
            CommitMessage = title,
            Body = Body(input, version, installers.Count),
        };

        var draft = new PlanDraft
        {
            Kind = ChangePlanKind.WingetChange,
            Title = $"winget: {title}",
            Items = items,
            CustomChanges = change,
            VersionId = version,
            VersionString = version,
            BaseKeys = input.Kind == WingetChangeKind.Listing ? [WingetContent.VersionsKey, WingetContent.BaseKey, WingetContent.ReadPrefix + version] : [WingetContent.VersionsKey, WingetContent.BaseKey],
            RequiredPermission = OrgPermission.SubmitForReview,
            ConfirmText = $"{input.PackageIdentifier} {version}",
            Calls = Calls(change),
        };

        return draft;
    }

    private static List<PlanCall> Calls(WingetPlannedChange change)
    {
        var calls = new List<PlanCall>();

        if (change.Installers.Count > 0)
            calls.AddRange(change.Installers.Select(i => new PlanCall { Method = "GET", Path = i.Url, Target = i.ExpectedSha256 == null ? "SHA256" : "SHA256 check" }));

        calls.Add(new PlanCall { Method = "POST", Path = "/repos/{fork}/merge-upstream", Target = "fork" });
        calls.Add(new PlanCall { Method = "POST", Path = "/repos/{fork}/git/trees", Target = $"{change.Files.Count} files" });
        calls.Add(new PlanCall { Method = "POST", Path = "/repos/{fork}/git/commits", Target = "fork" });
        calls.Add(new PlanCall { Method = "POST", Path = "/repos/{fork}/git/refs", Target = change.Branch });
        calls.Add(new PlanCall { Method = "POST", Path = "/repos/microsoft/winget-pkgs/pulls", Target = change.Title });

        return calls;
    }

    private static string Body(WingetPlanInput input, string version, int installers)
    {
        var builder = new StringBuilder();

        builder.Append("## Description\n\n");
        builder.Append(input.Kind == WingetChangeKind.Listing
            ? $"Locale text update of {input.PackageIdentifier} version {version}.\n\n"
            : $"{input.PackageIdentifier} version {version} with {installers} installer{(installers == 1 ? string.Empty : "s")}.\n\n");
        builder.Append("## Manifest Checklist\n\n");
        builder.Append("- [x] This PR only modifies one (1) manifest\n");
        builder.Append($"- [x] Manifests are multi-file manifests of schema {WingetFields.DefaultManifestVersion}, checked against the schema before the PR was opened\n");
        builder.Append("- [x] Installer hashes were computed from the downloads\n");
        builder.Append("- [ ] Tested manifest locally with `winget install --manifest <path>`\n\n");
        builder.Append("Opened with storeasy after the package owner approved the change.\n");

        return builder.ToString();
    }

    private static string TextOf(YamlMap map, string name)
    {
        if (map == null)
            return null;

        return name == "Tags" ? (map.Seq("Tags") is { Count: > 0 } tags ? string.Join(", ", tags.OfType<string>()) : null) : map.Text(name);
    }

    private static string Normalize(string text) => text?.Replace("\r\n", "\n").Trim();

    // Whether a manifest of winget-pkgs says the same as its rewrite (comments and layout aside).
    private static bool Unchanged(string original, string rewritten)
    {
        if (original == null)
            return false;

        try
        {
            var map = WingetYaml.Parse(original);

            return WingetManifestSet.Render(map, map.Text("ManifestType")) == rewritten;
        }
        catch (WingetYamlException)
        {
            return false;
        }
    }

    private static string SafeBranch(string branch)
    {
        var builder = new StringBuilder();

        foreach (var c in branch)
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '/' ? c : '-');

        return builder.ToString().Replace("..", ".", StringComparison.Ordinal);
    }
}
