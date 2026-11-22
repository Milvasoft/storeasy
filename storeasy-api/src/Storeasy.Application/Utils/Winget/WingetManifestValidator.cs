namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// One problem found in a manifest set: a message key with its arguments, where it is, and whether it stops the pull
/// request (a blocking issue is what the validation pipeline would reject).
/// </summary>
/// <param name="Key">Message key.</param>
/// <param name="Args">Message arguments.</param>
/// <param name="Field">Manifest field.</param>
/// <param name="Locale">Locale, for a locale manifest.</param>
/// <param name="Blocking">Whether the plan cannot be applied.</param>
public sealed record WingetIssue(string Key, object[] Args, string Field = null, string Locale = null, bool Blocking = true);

/// <summary>
/// Checks a manifest set against the manifest schema (<see cref="WingetFields"/>) and the winget-pkgs policies storeasy can
/// check without running the installer: multi-file manifests only, one identifier and version across the files,
/// unattended installers, no duplicate installer entries, https installer addresses on the publisher's own domain.
/// Running the installer is the validation pipeline's job.
/// </summary>
public static class WingetManifestValidator
{
    /// <summary>
    /// Placeholder of an installer hash computed when the plan is applied.
    /// </summary>
    public const string PendingSha256 = "<computed-on-apply>";

    /// <summary>
    /// Every issue of <paramref name="set"/>.
    /// </summary>
    /// <param name="set"></param>
    /// <returns></returns>
    public static List<WingetIssue> Validate(WingetManifestSet set)
    {
        var issues = new List<WingetIssue>();

        foreach (var (fileName, problem) in set.Unreadable)
            issues.Add(new(MessageKey.WingetManifestUnreadable, [fileName, problem]));

        ValidateVersion(set, issues);
        ValidateLocale(set, set.DefaultLocale, set.DefaultLocaleTag, true, issues);

        foreach (var (locale, manifest) in set.Locales)
            ValidateLocale(set, manifest, locale, false, issues);

        ValidateInstallers(set, issues);

        return issues;
    }

    private static void ValidateVersion(WingetManifestSet set, List<WingetIssue> issues)
    {
        var version = set.Version;

        Required(version, "PackageIdentifier", WingetManifestSet.VersionType, issues);
        Required(version, "PackageVersion", WingetManifestSet.VersionType, issues);
        Required(version, "DefaultLocale", WingetManifestSet.VersionType, issues);
        Required(version, "ManifestVersion", WingetManifestSet.VersionType, issues);

        if (set.PackageIdentifier is { } id && (id.Length > 128 || !WingetFields.PackageIdentifier().IsMatch(id)))
            issues.Add(new(MessageKey.WingetFieldInvalid, ["PackageIdentifier", id], "PackageIdentifier"));

        if (set.PackageVersion is { } packageVersion && (packageVersion.Length > 128 || !WingetFields.PackageVersion().IsMatch(packageVersion)))
            issues.Add(new(MessageKey.WingetFieldInvalid, ["PackageVersion", packageVersion], "PackageVersion"));

        if (set.DefaultLocaleTag is { } locale && !WingetFields.Locale().IsMatch(locale))
            issues.Add(new(MessageKey.WingetFieldInvalid, ["DefaultLocale", locale], "DefaultLocale"));

        if (set.ManifestVersion is { } manifestVersion)
        {
            if (!WingetFields.ManifestVersion().IsMatch(manifestVersion))
                issues.Add(new(MessageKey.WingetFieldInvalid, ["ManifestVersion", manifestVersion], "ManifestVersion"));
            else if (WingetVersions.Comparer.Compare(manifestVersion, WingetFields.MinimumManifestVersion) < 0)
                issues.Add(new(MessageKey.WingetManifestVersionOld, [manifestVersion, WingetFields.DefaultManifestVersion], "ManifestVersion", Blocking: false));
        }

        if (set.DefaultLocale.Count == 0)
            issues.Add(new(MessageKey.WingetFieldRequired, ["PackageLocale", WingetManifestSet.DefaultLocaleType], "DefaultLocale"));

        if (set.Installer.Count == 0)
            issues.Add(new(MessageKey.WingetFieldRequired, ["Installers", WingetManifestSet.InstallerType], "Installers"));

        var manifests = new List<(YamlMap Map, string Type)> { (set.Installer, WingetManifestSet.InstallerType), (set.DefaultLocale, WingetManifestSet.DefaultLocaleType) };

        manifests.AddRange(set.Locales.Values.Select(m => (m, WingetManifestSet.LocaleType)));

        foreach (var (map, type) in manifests.Where(m => m.Map.Count > 0))
        {
            foreach (var key in new[] { "PackageIdentifier", "PackageVersion", "ManifestVersion" })
            {
                if (!string.Equals(map.Text(key), version.Text(key), StringComparison.Ordinal))
                    issues.Add(new(MessageKey.WingetFieldMismatch, [key, type], key));
            }

            if (map.Text("ManifestType") != type)
                issues.Add(new(MessageKey.WingetFieldInvalid, ["ManifestType", map.Text("ManifestType") ?? "-"], "ManifestType"));
        }

        if (version.Text("ManifestType") != WingetManifestSet.VersionType)
            issues.Add(new(MessageKey.WingetFieldInvalid, ["ManifestType", version.Text("ManifestType") ?? "-"], "ManifestType"));
    }

    private static void ValidateLocale(WingetManifestSet set, YamlMap manifest, string locale, bool isDefault, List<WingetIssue> issues)
    {
        if (manifest.Count == 0)
            return;

        var tag = manifest.Text("PackageLocale");

        if (tag == null)
            issues.Add(new(MessageKey.WingetFieldRequired, ["PackageLocale", isDefault ? WingetManifestSet.DefaultLocaleType : WingetManifestSet.LocaleType], "PackageLocale", locale));
        else if (tag.Length > 20 || !WingetFields.Locale().IsMatch(tag))
            issues.Add(new(MessageKey.WingetFieldInvalid, ["PackageLocale", tag], "PackageLocale", locale));
        else if (isDefault && !string.Equals(tag, set.DefaultLocaleTag, StringComparison.OrdinalIgnoreCase))
            issues.Add(new(MessageKey.WingetFieldMismatch, ["DefaultLocale", WingetManifestSet.DefaultLocaleType], "PackageLocale", locale));

        foreach (var field in WingetFields.ListingFields)
        {
            var value = manifest.Text(field.Name);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (isDefault && field.RequiredInDefault)
                    issues.Add(new(MessageKey.WingetFieldRequired, [field.Name, WingetManifestSet.DefaultLocaleType], field.Name, locale));

                continue;
            }

            if (value.Length > field.MaxLength)
                issues.Add(new(MessageKey.WingetFieldTooLong, [field.Name, field.MaxLength], field.Name, locale));
            else if (value.Length < field.MinLength)
                issues.Add(new(MessageKey.WingetFieldTooShort, [field.Name, field.MinLength], field.Name, locale));

            if (field.IsUrl && !WingetFields.Url().IsMatch(value))
                issues.Add(new(MessageKey.WingetFieldInvalid, [field.Name, value], field.Name, locale));
        }

        if (manifest.Seq("Tags") is { } tags)
        {
            var texts = tags.OfType<string>().ToList();

            if (texts.Count > WingetFields.MaxTags)
                issues.Add(new(MessageKey.WingetTooManyTags, [WingetFields.MaxTags], "Tags", locale));

            if (texts.Any(t => t.Length > WingetFields.TagMaxLength))
                issues.Add(new(MessageKey.WingetFieldTooLong, ["Tags", WingetFields.TagMaxLength], "Tags", locale));

            if (texts.Distinct(StringComparer.Ordinal).Count() != texts.Count)
                issues.Add(new(MessageKey.WingetFieldInvalid, ["Tags", string.Join(", ", texts.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key))], "Tags", locale));
        }
    }

    private static void ValidateInstallers(WingetManifestSet set, List<WingetIssue> issues)
    {
        var manifest = set.Installer;

        if (manifest.Count == 0)
            return;

        var installers = manifest.Seq("Installers")?.OfType<YamlMap>().ToList() ?? [];

        if (installers.Count == 0)
        {
            issues.Add(new(MessageKey.WingetNoInstallers, [], "Installers"));
            return;
        }

        if (manifest.Text("MinimumOSVersion") is { } rootOs && !WingetFields.OsVersion().IsMatch(rootOs))
            issues.Add(new(MessageKey.WingetFieldInvalid, ["MinimumOSVersion", rootOs], "MinimumOSVersion"));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var publisherHosts = new[] { set.DefaultLocale.Text("PublisherUrl"), set.DefaultLocale.Text("PackageUrl"), set.DefaultLocale.Text("PublisherSupportUrl") }
                                 .Select(HostOf).Where(h => h != null).ToList();

        foreach (var installer in installers)
        {
            var architecture = installer.Text("Architecture");
            var type = installer.Text("InstallerType") ?? manifest.Text("InstallerType");
            var scope = installer.Text("Scope") ?? manifest.Text("Scope");
            var url = installer.Text("InstallerUrl");
            var sha = installer.Text("InstallerSha256");
            var label = architecture ?? "?";

            if (architecture == null)
                issues.Add(new(MessageKey.WingetFieldRequired, ["Architecture", WingetManifestSet.InstallerType], "Architecture"));
            else if (!WingetFields.Architectures.Contains(architecture))
                issues.Add(new(MessageKey.WingetFieldInvalid, ["Architecture", architecture], "Architecture"));

            if (type == null)
                issues.Add(new(MessageKey.WingetFieldRequired, ["InstallerType", WingetManifestSet.InstallerType], "InstallerType"));
            else if (!WingetFields.InstallerTypes.Contains(type))
                issues.Add(new(MessageKey.WingetFieldInvalid, ["InstallerType", type], "InstallerType"));
            else if (!WingetFields.AllowedInstallerTypes.Contains(type))
                issues.Add(new(MessageKey.WingetInstallerTypeNotAllowed, [type], "InstallerType"));

            if (scope != null && !WingetFields.Scopes.Contains(scope))
                issues.Add(new(MessageKey.WingetFieldInvalid, ["Scope", scope], "Scope"));

            if (url == null)
            {
                issues.Add(new(MessageKey.WingetFieldRequired, ["InstallerUrl", WingetManifestSet.InstallerType], "InstallerUrl"));
            }
            else if (url.Length > 2048 || !WingetFields.Url().IsMatch(url))
            {
                issues.Add(new(MessageKey.WingetFieldInvalid, ["InstallerUrl", url], "InstallerUrl"));
            }
            else
            {
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    issues.Add(new(MessageKey.WingetInstallerHttp, [url], "InstallerUrl", Blocking: false));

                var host = HostOf(url);

                if (host != null && publisherHosts.Count > 0 && !publisherHosts.Any(p => SameSite(p, host)))
                    issues.Add(new(MessageKey.WingetInstallerDomain, [host, string.Join(", ", publisherHosts.Distinct())], "InstallerUrl", Blocking: false));
            }

            if (sha == null)
                issues.Add(new(MessageKey.WingetFieldRequired, ["InstallerSha256", WingetManifestSet.InstallerType], "InstallerSha256"));
            else if (sha != PendingSha256 && !WingetFields.Sha256().IsMatch(sha))
                issues.Add(new(MessageKey.WingetFieldInvalid, ["InstallerSha256", sha], "InstallerSha256"));

            var switches = installer.Map("InstallerSwitches") ?? manifest.Map("InstallerSwitches");

            foreach (var (key, value) in switches ?? [])
            {
                if (!WingetFields.Switches.TryGetValue(key, out var max))
                    issues.Add(new(MessageKey.WingetFieldInvalid, ["InstallerSwitches", key], "InstallerSwitches"));
                else if (value is string text && text.Length > max)
                    issues.Add(new(MessageKey.WingetFieldTooLong, [$"InstallerSwitches.{key}", max], "InstallerSwitches"));
            }

            if (type != null && WingetFields.NeedsSwitches.Contains(type) && string.IsNullOrWhiteSpace(switches?.Text("Silent")) && string.IsNullOrWhiteSpace(switches?.Text("SilentWithProgress")))
                issues.Add(new(MessageKey.WingetInstallerNotSilent, [label], "InstallerSwitches"));

            if (installer.Text("MinimumOSVersion") is { } os && !WingetFields.OsVersion().IsMatch(os))
                issues.Add(new(MessageKey.WingetFieldInvalid, ["MinimumOSVersion", os], "MinimumOSVersion"));

            var identity = string.Join('|', architecture, scope, type, installer.Text("InstallerLocale") ?? manifest.Text("InstallerLocale"));

            if (!seen.Add(identity))
                issues.Add(new(MessageKey.WingetInstallerDuplicate, [label, scope ?? "-"], "Installers"));
        }
    }

    private static void Required(YamlMap map, string key, string manifestType, List<WingetIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(map.Text(key)))
            issues.Add(new(MessageKey.WingetFieldRequired, [key, manifestType], key));
    }

    private static string HostOf(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri.IdnHost.ToLowerInvariant() : null;

    /// <summary>
    /// Whether two hosts belong to the same site: equal, or sharing their last two labels (<c>download.example.com</c> and
    /// <c>www.example.com</c>). A GitHub release asset counts as the publisher's when the publisher's address is the same
    /// GitHub host.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool SameSite(string a, string b)
    {
        static string Site(string host)
        {
            var labels = host.Split('.');

            return labels.Length <= 2 ? host : string.Join('.', labels[^2..]);
        }

        static string Normalize(string host) => host is "objects.githubusercontent.com" or "release-assets.githubusercontent.com" ? "github.com" : host;

        return string.Equals(Site(Normalize(a)), Site(Normalize(b)), StringComparison.OrdinalIgnoreCase);
    }
}
