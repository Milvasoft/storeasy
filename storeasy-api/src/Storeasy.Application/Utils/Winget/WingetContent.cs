using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Domain.Content;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// The winget package as a store content snapshot (what the shared content reads, readiness and change plan conflict
/// checks work on): listing texts as <c>Listing|locale|Field</c> values, the version list and the raw manifest files of the
/// version read, plus a hash of those files that a plan's approval compares.
/// </summary>
public static class WingetContent
{
    /// <summary>
    /// Value key of the version list (highest first, comma separated).
    /// </summary>
    public const string VersionsKey = "winget.versions";

    /// <summary>
    /// Value key of the hash of the read version's manifest files.
    /// </summary>
    public const string BaseKey = "winget.base";

    /// <summary>
    /// Value key prefix of a manifest file of the read version.
    /// </summary>
    public const string FilePrefix = "winget.file|";

    /// <summary>
    /// Base key prefix naming the version a plan was built on (a text edit of an older version); its value is never set,
    /// the key only tells the approval which version to read again.
    /// </summary>
    public const string ReadPrefix = "winget.read|";

    /// <summary>
    /// Version state of a version in winget-pkgs.
    /// </summary>
    public const string PublishedState = "PUBLISHED";

    /// <summary>
    /// A snapshot of <paramref name="version"/> (null: the package has no version yet).
    /// </summary>
    /// <param name="packageIdentifier"></param>
    /// <param name="versions">Every version, highest first.</param>
    /// <param name="version"></param>
    /// <param name="files">Manifest files of <paramref name="version"/>.</param>
    /// <returns></returns>
    public static StoreContentSnapshot ToSnapshot(string packageIdentifier, IReadOnlyList<string> versions, string version, IReadOnlyDictionary<string, string> files)
    {
        var snapshot = new StoreContentSnapshot
        {
            Store = StoreKind.Winget,
            ExternalId = packageIdentifier,
            VersionId = version,
            VersionString = version,
            VersionState = version == null ? null : PublishedState,
            VersionEditable = version != null,
            HasLiveVersion = versions.Count > 0,
            Platform = "windows",
            Versions = [.. versions.Select(v => new StoreVersionState { Id = v, VersionString = v, Platform = "windows", State = PublishedState })],
        };

        snapshot.Values[VersionsKey] = string.Join(',', versions);
        snapshot.Values[BaseKey] = Hash(files ?? new Dictionary<string, string>());

        foreach (var (name, text) in files ?? new Dictionary<string, string>())
            snapshot.Values[FilePrefix + name] = text;

        var set = WingetManifestSet.FromFiles(files ?? new Dictionary<string, string>());

        snapshot.PrimaryLocale = set.DefaultLocaleTag;
        snapshot.Locales = [.. set.LocaleTags];

        foreach (var locale in set.LocaleTags)
        {
            var manifest = set.Locale(locale);

            foreach (var field in WingetFields.ListingFields)
            {
                if (manifest.Text(field.Name) is { } value)
                    snapshot.Values[ContentKey.Of(ContentGroups.Listing, locale, field.Name)] = value;
            }

            if (manifest.Seq("Tags") is { } tags)
                snapshot.Values[ContentKey.Of(ContentGroups.Listing, locale, "Tags")] = string.Join(',', tags.OfType<string>());
        }

        return snapshot;
    }

    /// <summary>
    /// Versions of a snapshot, highest first.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public static List<string> VersionsOf(StoreContentSnapshot snapshot)
        => snapshot.Get(VersionsKey) is { Length: > 0 } text ? [.. text.Split(',')] : [];

    /// <summary>
    /// Manifest files of a snapshot (file name to YAML).
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public static Dictionary<string, string> FilesOf(StoreContentSnapshot snapshot)
        => snapshot.Values.Where(v => v.Key.StartsWith(FilePrefix, StringComparison.Ordinal)).ToDictionary(v => v.Key[FilePrefix.Length..], v => v.Value, StringComparer.Ordinal);

    /// <summary>
    /// Hash of manifest files, independent of their order.
    /// </summary>
    /// <param name="files"></param>
    /// <returns></returns>
    public static string Hash(IReadOnlyDictionary<string, string> files)
    {
        var builder = new StringBuilder();

        foreach (var (name, text) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
            builder.Append(name).Append('\n').Append(text?.Replace("\r\n", "\n")).Append('\n');

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    /// The manifests of a snapshot for the API, with storeasy's checks localized.
    /// </summary>
    /// <param name="storeAppId"></param>
    /// <param name="snapshot"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static WingetManifestDto ToDto(int storeAppId, StoreContentSnapshot snapshot, IMilvaLocalizer localizer)
    {
        var files = FilesOf(snapshot);
        var set = WingetManifestSet.FromFiles(files);
        var dto = new WingetManifestDto
        {
            StoreAppId = storeAppId,
            PackageIdentifier = snapshot.ExternalId,
            Version = snapshot.VersionId,
            Versions = VersionsOf(snapshot),
            DefaultLocale = set.DefaultLocaleTag,
            ManifestVersion = set.ManifestVersion,
            Files = [.. files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => new WingetFileDto { FileName = f.Key, Content = f.Value })],
            Installers = InstallersOf(set),
        };

        foreach (var locale in set.LocaleTags)
        {
            var manifest = set.Locale(locale);

            dto.Locales.Add(new WingetLocaleTextsDto
            {
                Locale = locale,
                IsDefault = ReferenceEquals(manifest, set.DefaultLocale),
                Fields = WingetFields.ListingFields.Where(f => manifest.Text(f.Name) != null).ToDictionary(f => f.Name, f => manifest.Text(f.Name)),
                Tags = manifest.Seq("Tags")?.OfType<string>().ToList() ?? [],
            });
        }

        if (files.Count > 0)
            dto.Issues = Localize(WingetManifestValidator.Validate(set), localizer);

        return dto;
    }

    /// <summary>
    /// Installer entries of a set, with the manifest level values filled in.
    /// </summary>
    /// <param name="set"></param>
    /// <returns></returns>
    public static List<WingetInstallerDto> InstallersOf(WingetManifestSet set)
    {
        var root = set.Installer;

        return [.. (root.Seq("Installers") ?? []).OfType<YamlMap>().Select(i =>
        {
            var switches = i.Map("InstallerSwitches") ?? root.Map("InstallerSwitches");

            return new WingetInstallerDto
            {
                Architecture = i.Text("Architecture"),
                Type = i.Text("InstallerType") ?? root.Text("InstallerType"),
                Scope = i.Text("Scope") ?? root.Text("Scope"),
                Url = i.Text("InstallerUrl"),
                Sha256 = i.Text("InstallerSha256"),
                SilentSwitches = switches?.Text("Silent"),
                SilentWithProgressSwitches = switches?.Text("SilentWithProgress"),
                ProductCode = i.Text("ProductCode") ?? root.Text("ProductCode"),
                Locale = i.Text("InstallerLocale") ?? root.Text("InstallerLocale"),
            };
        })];
    }

    /// <summary>
    /// Localized issues.
    /// </summary>
    /// <param name="issues"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static List<WingetIssueDto> Localize(IEnumerable<WingetIssue> issues, IMilvaLocalizer localizer)
        => [.. issues.Select(i => new WingetIssueDto { Key = i.Key, Message = localizer == null ? i.Key : (string)localizer[i.Key, i.Args], Field = i.Field, Locale = i.Locale, Blocking = i.Blocking })];
}
