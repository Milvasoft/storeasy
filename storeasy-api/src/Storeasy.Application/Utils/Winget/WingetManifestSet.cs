namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// One version of a package as a multi-file manifest (the only kind winget-pkgs accepts): a version manifest, a default
/// locale manifest, an installer manifest and any number of locale manifests. Fields are kept as parsed, so a rewrite
/// keeps what storeasy does not edit.
/// </summary>
public sealed class WingetManifestSet
{
    /// <summary>
    /// Manifest type of the version manifest.
    /// </summary>
    public const string VersionType = "version";

    /// <summary>
    /// Manifest type of the default locale manifest.
    /// </summary>
    public const string DefaultLocaleType = "defaultLocale";

    /// <summary>
    /// Manifest type of the installer manifest.
    /// </summary>
    public const string InstallerType = "installer";

    /// <summary>
    /// Manifest type of a locale manifest.
    /// </summary>
    public const string LocaleType = "locale";

    /// <summary>
    /// Manifest type of a single file manifest (not allowed in winget-pkgs).
    /// </summary>
    public const string SingletonType = "singleton";

    /// <summary>
    /// The version manifest.
    /// </summary>
    public YamlMap Version { get; set; } = [];

    /// <summary>
    /// The installer manifest.
    /// </summary>
    public YamlMap Installer { get; set; } = [];

    /// <summary>
    /// The default locale manifest.
    /// </summary>
    public YamlMap DefaultLocale { get; set; } = [];

    /// <summary>
    /// Other locale manifests by locale tag.
    /// </summary>
    public SortedDictionary<string, YamlMap> Locales { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Files that could not be read or have an unknown type, with the reason (kept for the readiness report).
    /// </summary>
    public List<(string FileName, string Problem)> Unreadable { get; } = [];

    /// <summary>
    /// Whether the set came from a single file (singleton) manifest.
    /// </summary>
    public bool WasSingleton { get; set; }

    /// <summary>
    /// <c>PackageIdentifier</c> of the version manifest.
    /// </summary>
    public string PackageIdentifier => Version.Text("PackageIdentifier");

    /// <summary>
    /// <c>PackageVersion</c> of the version manifest.
    /// </summary>
    public string PackageVersion => Version.Text("PackageVersion");

    /// <summary>
    /// <c>DefaultLocale</c> of the version manifest.
    /// </summary>
    public string DefaultLocaleTag => Version.Text("DefaultLocale") ?? DefaultLocale.Text("PackageLocale");

    /// <summary>
    /// <c>ManifestVersion</c> of the version manifest.
    /// </summary>
    public string ManifestVersion => Version.Text("ManifestVersion");

    /// <summary>
    /// Every locale tag, the default first.
    /// </summary>
    public IEnumerable<string> LocaleTags => new[] { DefaultLocaleTag }.Where(t => t != null).Concat(Locales.Keys);

    /// <summary>
    /// The locale manifest of <paramref name="locale"/> (the default one included), or null.
    /// </summary>
    /// <param name="locale"></param>
    /// <returns></returns>
    public YamlMap Locale(string locale)
        => string.Equals(locale, DefaultLocaleTag, StringComparison.OrdinalIgnoreCase) ? DefaultLocale : Locales.GetValueOrDefault(locale ?? string.Empty);

    /// <summary>
    /// A deep copy.
    /// </summary>
    /// <returns></returns>
    public WingetManifestSet Clone()
    {
        var copy = new WingetManifestSet { Version = Version.Clone(), Installer = Installer.Clone(), DefaultLocale = DefaultLocale.Clone(), WasSingleton = WasSingleton };

        foreach (var (locale, map) in Locales)
            copy.Locales[locale] = map.Clone();

        return copy;
    }

    /// <summary>
    /// Reads the files of one version folder (file name to YAML). A singleton manifest is split into the three manifests
    /// so it can be rewritten as a multi-file one.
    /// </summary>
    /// <param name="files"></param>
    /// <returns></returns>
    public static WingetManifestSet FromFiles(IEnumerable<KeyValuePair<string, string>> files)
    {
        var set = new WingetManifestSet();

        foreach (var (fileName, text) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            YamlMap map;

            try
            {
                map = WingetYaml.Parse(text);
            }
            catch (WingetYamlException ex)
            {
                set.Unreadable.Add((fileName, ex.Message));
                continue;
            }

            switch (map.Text("ManifestType"))
            {
                case VersionType:
                    set.Version = map;
                    break;
                case InstallerType:
                    set.Installer = map;
                    break;
                case DefaultLocaleType:
                    set.DefaultLocale = map;
                    break;
                case LocaleType when map.Text("PackageLocale") is { } locale:
                    set.Locales[locale] = map;
                    break;
                case SingletonType:
                    set.WasSingleton = true;
                    SplitSingleton(set, map);
                    break;
                default:
                    set.Unreadable.Add((fileName, $"Unknown ManifestType '{map.Text("ManifestType")}'."));
                    break;
            }
        }

        return set;
    }

    /// <summary>
    /// The files of the set for a pull request: path inside winget-pkgs to YAML text, each with the schema header winget
    /// tools write.
    /// </summary>
    /// <returns></returns>
    public List<WingetManifestFile> ToFiles()
    {
        var id = PackageIdentifier;
        var folder = WingetPaths.VersionFolder(id, PackageVersion);
        var files = new List<WingetManifestFile>
        {
            new($"{folder}/{id}.yaml", Render(Version, VersionType)),
            new($"{folder}/{id}.installer.yaml", Render(Installer, InstallerType)),
            new($"{folder}/{id}.locale.{DefaultLocaleTag}.yaml", Render(DefaultLocale, DefaultLocaleType)),
        };

        files.AddRange(Locales.Select(l => new WingetManifestFile($"{folder}/{id}.locale.{l.Key}.yaml", Render(l.Value, LocaleType))));

        return files;
    }

    /// <summary>
    /// YAML of one manifest with the generator and schema comment lines.
    /// </summary>
    /// <param name="manifest"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string Render(YamlMap manifest, string type)
        => WingetYaml.Write(manifest, ["Created with storeasy", $"yaml-language-server: $schema={WingetPaths.SchemaUrl(type, manifest.Text("ManifestVersion"))}"]);

    private static void SplitSingleton(WingetManifestSet set, YamlMap map)
    {
        var version = new YamlMap();
        var installer = new YamlMap();
        var locale = new YamlMap();
        var common = new[] { "PackageIdentifier", "PackageVersion" };

        foreach (var key in common)
        {
            version.Set(key, map.Get(key));
            installer.Set(key, WingetYaml.CloneValue(map.Get(key)));
            locale.Set(key, WingetYaml.CloneValue(map.Get(key)));
        }

        foreach (var (key, value) in map)
        {
            if (common.Contains(key) || key is "ManifestType" or "ManifestVersion")
                continue;

            if (WingetFields.LocaleFields.Contains(key))
                locale.Set(key, value);
            else
                installer.Set(key, value);
        }

        var manifestVersion = map.Text("ManifestVersion");

        version.Set("DefaultLocale", locale.Text("PackageLocale"));
        version.Set("ManifestType", VersionType);
        version.Set("ManifestVersion", manifestVersion);
        installer.Set("ManifestType", InstallerType);
        installer.Set("ManifestVersion", manifestVersion);
        locale.Set("ManifestType", DefaultLocaleType);
        locale.Set("ManifestVersion", manifestVersion);

        set.Version = version;
        set.Installer = installer;
        set.DefaultLocale = locale;
    }
}

/// <summary>
/// One manifest file of a pull request.
/// </summary>
/// <param name="Path">Path inside winget-pkgs.</param>
/// <param name="Content">YAML text (LF line ends).</param>
public sealed record WingetManifestFile(string Path, string Content);

/// <summary>
/// Paths and schema addresses of winget-pkgs.
/// </summary>
public static class WingetPaths
{
    /// <summary>
    /// Folder of a package: <c>manifests/&lt;first letter, lower case&gt;/&lt;one folder per identifier segment&gt;</c>.
    /// </summary>
    /// <param name="packageIdentifier"></param>
    /// <returns></returns>
    public static string PackageFolder(string packageIdentifier)
        => $"manifests/{char.ToLowerInvariant(packageIdentifier[0])}/{packageIdentifier.Replace('.', '/')}";

    /// <summary>
    /// Folder of one version of a package.
    /// </summary>
    /// <param name="packageIdentifier"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public static string VersionFolder(string packageIdentifier, string version) => $"{PackageFolder(packageIdentifier)}/{version}";

    /// <summary>
    /// JSON schema address of a manifest type and schema version (the <c>yaml-language-server</c> line).
    /// </summary>
    /// <param name="type"></param>
    /// <param name="manifestVersion"></param>
    /// <returns></returns>
    public static string SchemaUrl(string type, string manifestVersion) => $"https://aka.ms/winget-manifest.{type}.{manifestVersion}.schema.json";

    /// <summary>
    /// Web page of a package folder in winget-pkgs.
    /// </summary>
    /// <param name="upstream">Upstream repository as <c>owner/name</c>.</param>
    /// <param name="packageIdentifier"></param>
    /// <returns></returns>
    public static string PackagePage(string upstream, string packageIdentifier) => $"https://github.com/{upstream}/tree/master/{PackageFolder(packageIdentifier)}";
}
