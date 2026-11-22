using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Intake;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace Storeasy.Application.Utils.Chrome;

/// <summary>
/// One problem found in an extension package or a Chrome plan. A blocking problem stops the plan (and rejects the package at
/// intake); the others are warnings.
/// </summary>
/// <param name="Key">Message key; its text takes <paramref name="Args"/>.</param>
/// <param name="Blocking">Whether it stops the plan.</param>
/// <param name="Args">Message arguments.</param>
public sealed record ChromeIssue(string Key, bool Blocking, params object[] Args)
{
    /// <summary>
    /// English text of the issue, for intake checks and job messages.
    /// </summary>
    public string English => string.Format(CultureInfo.InvariantCulture, ChromeManifest.EnglishOf(this.Key), this.Args);
}

/// <summary>
/// What storeasy reads from the manifest.json of a browser extension ZIP, and the offline checks of the Chrome Web Store's
/// package rules: Manifest V3, a valid version, the name and description limits of the store, a default locale for
/// localized names, a 128 pixel icon. The store runs its own checks again when the package is uploaded.
/// </summary>
public sealed class ChromeManifest
{
    /// <summary>
    /// Most characters of the name the store shows.
    /// </summary>
    public const int NameMaxLength = 75;

    /// <summary>
    /// Most characters of the manifest description.
    /// </summary>
    public const int DescriptionMaxLength = 132;

    /// <summary>
    /// Largest package the store takes.
    /// </summary>
    public const long MaxPackageBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// Largest manifest.json storeasy reads.
    /// </summary>
    public const int MaxManifestBytes = 1024 * 1024;

    private static readonly string[] _broadHosts = ["<all_urls>", "*://*/*", "http://*/*", "https://*/*"];

    private const char _argSeparator = '';

    private static readonly JsonDocumentOptions _options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>
    /// <c>manifest_version</c>.
    /// </summary>
    public int? ManifestVersion { get; set; }

    /// <summary>
    /// <c>name</c> (may be a <c>__MSG_name__</c> placeholder).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// <c>version</c>.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// <c>version_name</c>, shown instead of the version.
    /// </summary>
    public string VersionName { get; set; }

    /// <summary>
    /// <c>description</c>.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// <c>default_locale</c>.
    /// </summary>
    public string DefaultLocale { get; set; }

    /// <summary>
    /// <c>minimum_chrome_version</c>.
    /// </summary>
    public string MinimumChromeVersion { get; set; }

    /// <summary>
    /// <c>permissions</c> and <c>optional_permissions</c>.
    /// </summary>
    public List<string> Permissions { get; set; } = [];

    /// <summary>
    /// <c>host_permissions</c>, <c>optional_host_permissions</c> and the <c>matches</c> of content scripts.
    /// </summary>
    public List<string> HostPermissions { get; set; } = [];

    /// <summary>
    /// Whether <c>icons</c> has a 128 pixel icon.
    /// </summary>
    public bool HasIcon128 { get; set; }

    /// <summary>
    /// Whether <c>background</c> uses <c>scripts</c> or <c>page</c> (Manifest V2 background pages).
    /// </summary>
    public bool HasBackgroundPage { get; set; }

    /// <summary>
    /// <c>update_url</c>.
    /// </summary>
    public string UpdateUrl { get; set; }

    /// <summary>
    /// Folder that holds manifest.json when it is not at the root of the ZIP (a zipped folder), else null.
    /// </summary>
    public string NestedFolder { get; set; }

    /// <summary>
    /// Reads manifest.json from the root of an extension ZIP. A ZIP without one returns a manifest with
    /// <see cref="NestedFolder"/> set when it is one folder down, or throws <see cref="InvalidDataException"/>.
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ChromeManifest> ReadAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("manifest.json");

        if (entry == null)
        {
            var nested = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/manifest.json", StringComparison.Ordinal) && e.FullName.Count(c => c == '/') == 1);

            if (nested == null)
                throw new InvalidDataException("The ZIP has no manifest.json at its root.");

            return new ChromeManifest { NestedFolder = nested.FullName[..nested.FullName.IndexOf('/')] };
        }

        if (entry.Length > MaxManifestBytes)
            throw new InvalidDataException("manifest.json is larger than 1 MB.");

        await using var stream = await entry.OpenAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(cancellationToken);

        return Parse(text);
    }

    /// <summary>
    /// Parses the text of a manifest.json (comments and trailing commas allowed, as Chrome allows them). Throws
    /// <see cref="InvalidDataException"/> for text that is not a JSON object.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static ChromeManifest Parse(string text)
    {
        JsonElement root;

        try
        {
            root = JsonDocument.Parse(text.TrimStart('﻿'), _options).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"manifest.json is not valid JSON: {ex.Message}");
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("manifest.json is not a JSON object.");

        var manifest = new ChromeManifest
        {
            ManifestVersion = root.TryGetProperty("manifest_version", out var mv) && mv.ValueKind == JsonValueKind.Number && mv.TryGetInt32(out var number) ? number : null,
            Name = Text(root, "name"),
            Version = Text(root, "version"),
            VersionName = Text(root, "version_name"),
            Description = Text(root, "description"),
            DefaultLocale = Text(root, "default_locale"),
            MinimumChromeVersion = Text(root, "minimum_chrome_version"),
            UpdateUrl = Text(root, "update_url"),
            Permissions = [.. Strings(root, "permissions").Concat(Strings(root, "optional_permissions")).Distinct(StringComparer.Ordinal)],
        };

        var hosts = Strings(root, "host_permissions").Concat(Strings(root, "optional_host_permissions")).ToList();

        if (root.TryGetProperty("content_scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Array)
        {
            foreach (var script in scripts.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object))
                hosts.AddRange(Strings(script, "matches"));
        }

        manifest.HostPermissions = [.. hosts.Distinct(StringComparer.Ordinal)];
        manifest.HasIcon128 = root.TryGetProperty("icons", out var icons) && icons.ValueKind == JsonValueKind.Object && icons.TryGetProperty("128", out var icon) && icon.ValueKind == JsonValueKind.String;
        manifest.HasBackgroundPage = root.TryGetProperty("background", out var background) && background.ValueKind == JsonValueKind.Object
                                     && (background.TryGetProperty("scripts", out _) || background.TryGetProperty("page", out _));

        return manifest;
    }

    /// <summary>
    /// The offline package checks. <paramref name="higherThan"/> lists the versions the package must be above (published,
    /// submitted, uploaded), with what each is.
    /// </summary>
    /// <param name="higherThan"></param>
    /// <returns></returns>
    public List<ChromeIssue> Check(IEnumerable<(string Version, string What)> higherThan = null)
    {
        var issues = new List<ChromeIssue>();

        if (NestedFolder != null)
        {
            issues.Add(new ChromeIssue(MessageKey.ChromeManifestNested, true, NestedFolder));

            return issues;
        }

        if (ManifestVersion != 3)
            issues.Add(new ChromeIssue(MessageKey.ChromeManifestVersion, true, ManifestVersion?.ToString(CultureInfo.InvariantCulture) ?? "-"));

        if (string.IsNullOrWhiteSpace(Name))
            issues.Add(new ChromeIssue(MessageKey.ChromeNameMissing, true));
        else if (!IsMessage(Name) && Name.Length > NameMaxLength)
            issues.Add(new ChromeIssue(MessageKey.ChromeTextTooLong, true, "name", Name.Length, NameMaxLength));

        if (Description != null && !IsMessage(Description) && Description.Length > DescriptionMaxLength)
            issues.Add(new ChromeIssue(MessageKey.ChromeTextTooLong, true, "description", Description.Length, DescriptionMaxLength));

        if ((IsMessage(Name) || IsMessage(Description)) && string.IsNullOrWhiteSpace(DefaultLocale))
            issues.Add(new ChromeIssue(MessageKey.ChromeDefaultLocaleMissing, true));

        if (!ChromeVersions.IsValid(Version))
        {
            issues.Add(new ChromeIssue(MessageKey.ChromeVersionInvalid, true, Version ?? "-"));
        }
        else
        {
            foreach (var (version, what) in higherThan ?? [])
            {
                if (ChromeVersions.IsValid(version) && ChromeVersions.Compare(Version, version) <= 0)
                    issues.Add(new ChromeIssue(MessageKey.ChromeVersionNotHigher, true, Version, version, what));
            }
        }

        if (HasBackgroundPage)
            issues.Add(new ChromeIssue(MessageKey.ChromeBackgroundPage, false));

        if (!HasIcon128)
            issues.Add(new ChromeIssue(MessageKey.ChromeIcon128Missing, false));

        if (!string.IsNullOrWhiteSpace(UpdateUrl) && !UpdateUrl.Contains("clients2.google.com", StringComparison.OrdinalIgnoreCase))
            issues.Add(new ChromeIssue(MessageKey.ChromeUpdateUrl, false, UpdateUrl));

        var broad = HostPermissions.Where(h => _broadHosts.Contains(h, StringComparer.Ordinal)).ToList();

        if (broad.Count > 0)
            issues.Add(new ChromeIssue(MessageKey.ChromeBroadHostAccess, false, string.Join(", ", broad)));

        return issues;
    }

    /// <summary>
    /// The intake view of the manifest.
    /// </summary>
    /// <param name="higherThan"></param>
    /// <returns></returns>
    public ArtifactPackageInfo ToPackageInfo(IEnumerable<(string Version, string What)> higherThan = null)
    {
        var issues = Check(higherThan);

        return new ArtifactPackageInfo
        {
            Kind = PackageKind.ExtensionZip,
            Identifier = Name,
            DisplayName = VersionName == null ? Name : $"{Name} ({VersionName})",
            VersionName = Version,
            VersionCode = Version,
            MinOs = MinimumChromeVersion,
            ManifestVersion = ManifestVersion,
            Permissions = Permissions,
            HostPermissions = HostPermissions,
            ManifestIssues = [.. issues.Select(Encode)],
            Signature = "not signed (the store signs uploaded ZIPs)",
        };
    }

    /// <summary>
    /// The issues an intake package carries (<see cref="ArtifactPackageInfo.ManifestIssues"/>).
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    public static List<(bool Blocking, string Key, string Text, object[] Args)> IssuesOf(ArtifactPackageInfo package)
        => [.. (package?.ManifestIssues ?? []).Select(line =>
        {
            // "error:Key|English text|arg1<US>arg2"
            var blocking = line.StartsWith("error:", StringComparison.Ordinal);
            var parts = line[(line.IndexOf(':') + 1)..].Split('|');
            var args = parts.Length > 2 && parts[2].Length > 0 ? parts[2].Split(_argSeparator).Cast<object>().ToArray() : [];

            return (blocking, parts[0], parts.Length > 1 ? parts[1] : parts[0], args);
        })];

    /// <summary>
    /// The line an issue is kept as in <see cref="ArtifactPackageInfo.ManifestIssues"/>.
    /// </summary>
    /// <param name="issue"></param>
    /// <returns></returns>
    public static string Encode(ChromeIssue issue)
        => ManifestIssueLines.Encode(issue.Blocking, issue.Key, issue.English, issue.Args);

    /// <summary>
    /// Localized texts of <paramref name="issues"/>.
    /// </summary>
    /// <param name="issues"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static List<(ChromeIssue Issue, string Message)> Localize(IEnumerable<ChromeIssue> issues, IMilvaLocalizer localizer)
        => [.. issues.Select(i => (i, (string)localizer[i.Key, i.Args]))];

    /// <summary>
    /// English text of an issue key (intake checks and job messages are English, like the other stores').
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string EnglishOf(string key) => key switch
    {
        MessageKey.ChromeManifestNested => "manifest.json is in the folder {0}/: zip the contents of the folder, not the folder.",
        MessageKey.ChromeManifestVersion => "manifest_version is {0}; the Chrome Web Store accepts Manifest V3 packages only.",
        MessageKey.ChromeNameMissing => "manifest.json has no name.",
        MessageKey.ChromeTextTooLong => "{0} has {1} characters; the store allows {2}.",
        MessageKey.ChromeDefaultLocaleMissing => "The name or description is a __MSG_ placeholder but default_locale is missing.",
        MessageKey.ChromeVersionInvalid => "version \"{0}\" is not one to four dot separated numbers from 0 to 65535.",
        MessageKey.ChromeVersionNotHigher => "version {0} must be higher than {1} ({2}).",
        MessageKey.ChromeBackgroundPage => "background.scripts or background.page is a Manifest V2 background page; Chrome runs a service_worker in Manifest V3.",
        MessageKey.ChromeIcon128Missing => "icons has no 128 pixel icon; the store needs one in the package.",
        MessageKey.ChromeUpdateUrl => "update_url points to {0}; items in the Chrome Web Store are updated by the store.",
        MessageKey.ChromeBroadHostAccess => "Broad host access ({0}) needs a justification and may make the review longer.",
        _ => key,
    };

    private static bool IsMessage(string text) => text != null && text.StartsWith("__MSG_", StringComparison.Ordinal);

    private static string Text(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IEnumerable<string> Strings(JsonElement root, string name) => ManifestIssueLines.Strings(root, name);
}
