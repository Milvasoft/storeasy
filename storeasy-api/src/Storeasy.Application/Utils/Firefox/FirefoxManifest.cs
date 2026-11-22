using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Intake;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace Storeasy.Application.Utils.Firefox;

/// <summary>
/// One problem found in a Firefox package or a Firefox plan. A blocking problem stops the plan (and rejects the package at
/// intake); the others are warnings.
/// </summary>
/// <param name="Key">Message key; its text takes <paramref name="Args"/>.</param>
/// <param name="Blocking">Whether it stops the plan.</param>
/// <param name="Args">Message arguments.</param>
public sealed record FirefoxIssue(string Key, bool Blocking, params object[] Args)
{
    /// <summary>
    /// English text of the issue, for intake checks and job messages.
    /// </summary>
    public string English => string.Format(CultureInfo.InvariantCulture, FirefoxManifest.EnglishOf(this.Key), this.Args);
}

/// <summary>
/// What storeasy reads from the manifest.json of a Firefox extension (.xpi or .zip), and the offline checks of the AMO
/// package rules and add-on policies: Manifest V2 or V3, a valid version, an add-on id
/// (<c>browser_specific_settings.gecko.id</c>, required for Manifest V3), the data collection declaration
/// (<c>data_collection_permissions</c>), the name and summary limits, and whether the code looks minified or bundled (AMO then
/// requires the source code). AMO runs its own validator again when the package is uploaded.
/// </summary>
public sealed class FirefoxManifest
{
    /// <summary>
    /// Most characters of an add-on name on AMO.
    /// </summary>
    public const int NameMaxLength = 50;

    /// <summary>
    /// Most characters of an add-on summary on AMO (the manifest description becomes the summary of a new add-on).
    /// </summary>
    public const int SummaryMaxLength = 250;

    /// <summary>
    /// Largest manifest.json storeasy reads.
    /// </summary>
    public const int MaxManifestBytes = 1024 * 1024;

    /// <summary>
    /// A script line longer than this is taken as minified or bundled code.
    /// </summary>
    public const int MinifiedLineLength = 1000;

    /// <summary>
    /// Most scripts storeasy reads to look for minified code.
    /// </summary>
    public const int MaxScriptsScanned = 200;

    /// <summary>
    /// Largest script storeasy reads to look for minified code.
    /// </summary>
    public const int MaxScriptBytes = 4 * 1024 * 1024;

    private static readonly string[] _broadHosts = ["<all_urls>", "*://*/*", "http://*/*", "https://*/*"];

    private const char _argSeparator = '\u001f';

    private static readonly JsonDocumentOptions _options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary><c>manifest_version</c>.</summary>
    public int? ManifestVersion { get; set; }

    /// <summary><c>name</c> (may be a <c>__MSG_name__</c> placeholder).</summary>
    public string Name { get; set; }

    /// <summary><c>version</c>.</summary>
    public string Version { get; set; }

    /// <summary><c>description</c>.</summary>
    public string Description { get; set; }

    /// <summary><c>default_locale</c>.</summary>
    public string DefaultLocale { get; set; }

    /// <summary><c>browser_specific_settings.gecko.id</c> (or the older <c>applications.gecko.id</c>).</summary>
    public string GeckoId { get; set; }

    /// <summary><c>browser_specific_settings.gecko.strict_min_version</c>.</summary>
    public string StrictMinVersion { get; set; }

    /// <summary><c>browser_specific_settings.gecko.update_url</c> (self distribution).</summary>
    public string UpdateUrl { get; set; }

    /// <summary>Whether <c>browser_specific_settings.gecko.data_collection_permissions</c> is declared.</summary>
    public bool DeclaresDataCollection { get; set; }

    /// <summary>The required data collection permissions (<c>none</c> when the add-on collects nothing).</summary>
    public List<string> DataCollection { get; set; } = [];

    /// <summary><c>permissions</c> and <c>optional_permissions</c>.</summary>
    public List<string> Permissions { get; set; } = [];

    /// <summary><c>host_permissions</c>, <c>optional_host_permissions</c> and the <c>matches</c> of content scripts.</summary>
    public List<string> HostPermissions { get; set; } = [];

    /// <summary>Whether <c>icons</c> has at least one icon.</summary>
    public bool HasIcon { get; set; }

    /// <summary>Whether <c>background</c> has only a <c>service_worker</c> (Firefox runs <c>background.scripts</c>).</summary>
    public bool ServiceWorkerOnly { get; set; }

    /// <summary>Whether the package is signed already (<c>META-INF/mozilla.rsa</c>).</summary>
    public bool IsSigned { get; set; }

    /// <summary>Scripts that look minified or bundled (the first few).</summary>
    public List<string> MinifiedScripts { get; set; } = [];

    /// <summary>Folder that holds manifest.json when it is not at the root of the archive, else null.</summary>
    public string NestedFolder { get; set; }

    /// <summary>
    /// Reads manifest.json from the root of an extension archive and scans its scripts. An archive without one returns a
    /// manifest with <see cref="NestedFolder"/> set when it is one folder down, or throws <see cref="InvalidDataException"/>.
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<FirefoxManifest> ReadAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("manifest.json");

        if (entry == null)
        {
            var nested = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/manifest.json", StringComparison.Ordinal) && e.FullName.Count(c => c == '/') == 1);

            if (nested == null)
                throw new InvalidDataException("The archive has no manifest.json at its root.");

            return new FirefoxManifest { NestedFolder = nested.FullName[..nested.FullName.IndexOf('/')] };
        }

        if (entry.Length > MaxManifestBytes)
            throw new InvalidDataException("manifest.json is larger than 1 MB.");

        string text;

        await using (var stream = await entry.OpenAsync(cancellationToken))
        using (var reader = new StreamReader(stream))
        {
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        var manifest = Parse(text);

        manifest.IsSigned = archive.GetEntry("META-INF/mozilla.rsa") != null || archive.GetEntry("META-INF/cose.sig") != null;

        foreach (var script in archive.Entries.Where(e => e.FullName.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase))
                                              .Take(MaxScriptsScanned))
        {
            if (manifest.MinifiedScripts.Count >= 5)
                break;

            if (script.Name.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase) || (script.Length <= MaxScriptBytes && await HasLongLineAsync(script, cancellationToken)))
                manifest.MinifiedScripts.Add(script.FullName);
        }

        return manifest;
    }

    /// <summary>
    /// Parses the text of a manifest.json (comments and trailing commas allowed). Throws <see cref="InvalidDataException"/>
    /// for text that is not a JSON object.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static FirefoxManifest Parse(string text)
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

        var gecko = Child(Child(root, "browser_specific_settings"), "gecko") ?? Child(Child(root, "applications"), "gecko");
        var dataCollection = Child(gecko, "data_collection_permissions");

        var manifest = new FirefoxManifest
        {
            ManifestVersion = root.TryGetProperty("manifest_version", out var mv) && mv.ValueKind == JsonValueKind.Number && mv.TryGetInt32(out var number) ? number : null,
            Name = Text(root, "name"),
            Version = Text(root, "version"),
            Description = Text(root, "description"),
            DefaultLocale = Text(root, "default_locale"),
            GeckoId = gecko is { } g ? Text(g, "id") : null,
            StrictMinVersion = gecko is { } m ? Text(m, "strict_min_version") : null,
            UpdateUrl = gecko is { } u ? Text(u, "update_url") : null,
            DeclaresDataCollection = dataCollection is { ValueKind: JsonValueKind.Object },
            DataCollection = dataCollection is { } d ? [.. Strings(d, "required")] : [],
            Permissions = [.. Strings(root, "permissions").Concat(Strings(root, "optional_permissions")).Distinct(StringComparer.Ordinal)],
        };

        var hosts = Strings(root, "host_permissions").Concat(Strings(root, "optional_host_permissions")).ToList();

        // Manifest V2 keeps host patterns among the permissions.
        hosts.AddRange(manifest.Permissions.Where(p => p.Contains("://", StringComparison.Ordinal) || p == "<all_urls>"));

        if (root.TryGetProperty("content_scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Array)
        {
            foreach (var script in scripts.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object))
                hosts.AddRange(Strings(script, "matches"));
        }

        manifest.HostPermissions = [.. hosts.Distinct(StringComparer.Ordinal)];
        manifest.HasIcon = root.TryGetProperty("icons", out var icons) && icons.ValueKind == JsonValueKind.Object && icons.EnumerateObject().Any();

        if (Child(root, "background") is { } background)
            manifest.ServiceWorkerOnly = background.TryGetProperty("service_worker", out _) && !background.TryGetProperty("scripts", out _) && !background.TryGetProperty("page", out _);

        return manifest;
    }

    /// <summary>
    /// The offline package checks. <paramref name="higherThan"/> lists the versions the package must be above, with what each
    /// is; <paramref name="guid"/> is the add-on's guid the package must carry, when known.
    /// </summary>
    /// <param name="higherThan"></param>
    /// <param name="guid"></param>
    /// <returns></returns>
    public List<FirefoxIssue> Check(IEnumerable<(string Version, string What)> higherThan = null, string guid = null)
    {
        var issues = new List<FirefoxIssue>();

        if (NestedFolder != null)
        {
            issues.Add(new FirefoxIssue(MessageKey.FirefoxManifestNested, true, NestedFolder));

            return issues;
        }

        if (ManifestVersion is not (2 or 3))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxManifestVersion, true, ManifestVersion?.ToString(CultureInfo.InvariantCulture) ?? "-"));
        else if (ManifestVersion == 2)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxManifestV2, false));

        if (string.IsNullOrWhiteSpace(Name))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxNameMissing, true));
        else if (!IsMessage(Name) && Name.Length > NameMaxLength)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxTextTooLong, true, "name", Name.Length, NameMaxLength));

        if (Description != null && !IsMessage(Description) && Description.Length > SummaryMaxLength)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxTextTooLong, false, "description", Description.Length, SummaryMaxLength));

        if ((IsMessage(Name) || IsMessage(Description)) && string.IsNullOrWhiteSpace(DefaultLocale))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxDefaultLocaleMissing, true));

        if (!FirefoxVersions.IsValid(Version))
        {
            issues.Add(new FirefoxIssue(MessageKey.FirefoxVersionInvalid, true, Version ?? "-"));
        }
        else
        {
            foreach (var (version, what) in higherThan ?? [])
            {
                if (!string.IsNullOrEmpty(version) && FirefoxVersions.Compare(Version, version) <= 0)
                    issues.Add(new FirefoxIssue(MessageKey.FirefoxVersionNotHigher, true, Version, version, what));
            }
        }

        if (string.IsNullOrWhiteSpace(GeckoId))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxGeckoIdMissing, ManifestVersion == 3 || guid != null));
        else if (!FirefoxVersions.IsGuid(GeckoId))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxGeckoIdInvalid, true, GeckoId));
        else if (guid != null && !string.Equals(GeckoId, guid, StringComparison.Ordinal))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxGeckoIdMismatch, true, GeckoId, guid));

        if (!DeclaresDataCollection)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxDataCollectionMissing, false));

        if (!string.IsNullOrWhiteSpace(UpdateUrl))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxUpdateUrl, false, UpdateUrl));

        if (ServiceWorkerOnly)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxServiceWorker, false));

        if (!HasIcon)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxIconMissing, false));

        var broad = HostPermissions.Where(h => _broadHosts.Contains(h, StringComparer.Ordinal)).ToList();

        if (broad.Count > 0)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxBroadHostAccess, false, string.Join(", ", broad)));

        if (MinifiedScripts.Count > 0)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxSourceRequired, false, string.Join(", ", MinifiedScripts)));

        return issues;
    }

    /// <summary>
    /// The intake view of the manifest. The identifier is the add-on id, so intake matches the package with the store record
    /// whose bundle id is the add-on guid.
    /// </summary>
    /// <returns></returns>
    public ArtifactPackageInfo ToPackageInfo()
    {
        var issues = Check();

        return new ArtifactPackageInfo
        {
            Kind = PackageKind.Xpi,
            Identifier = GeckoId,
            DisplayName = Name,
            VersionName = Version,
            VersionCode = Version,
            MinOs = StrictMinVersion,
            ManifestVersion = ManifestVersion,
            Permissions = Permissions,
            HostPermissions = HostPermissions,
            ManifestIssues = [.. issues.Select(Encode)],
            Signature = IsSigned ? "signed" : "not signed (AMO signs uploaded versions)",
        };
    }

    /// <summary>
    /// The issues an intake package carries (<see cref="ArtifactPackageInfo.ManifestIssues"/>).
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    public static List<FirefoxIssue> IssuesOf(ArtifactPackageInfo package)
        => [.. (package?.ManifestIssues ?? []).Select(line =>
        {
            // "error:Key|English text|arg1<US>arg2"
            var blocking = line.StartsWith("error:", StringComparison.Ordinal);
            var parts = line[(line.IndexOf(':') + 1)..].Split('|');
            var args = parts.Length > 2 && parts[2].Length > 0 ? parts[2].Split(_argSeparator).Cast<object>().ToArray() : [];

            return new FirefoxIssue(parts[0], blocking, args);
        })];

    /// <summary>
    /// The line an issue is kept as in <see cref="ArtifactPackageInfo.ManifestIssues"/>.
    /// </summary>
    /// <param name="issue"></param>
    /// <returns></returns>
    public static string Encode(FirefoxIssue issue)
        => ManifestIssueLines.Encode(issue.Blocking, issue.Key, issue.English, issue.Args);

    /// <summary>
    /// Localized texts of <paramref name="issues"/>.
    /// </summary>
    /// <param name="issues"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static List<(FirefoxIssue Issue, string Message)> Localize(IEnumerable<FirefoxIssue> issues, IMilvaLocalizer localizer)
        => [.. issues.Select(i => (i, (string)localizer[i.Key, i.Args]))];

    /// <summary>
    /// English text of an issue key (intake checks and job messages are English, like the other stores').
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string EnglishOf(string key) => key switch
    {
        MessageKey.FirefoxManifestNested => "manifest.json is in the folder {0}/: zip the contents of the folder, not the folder.",
        MessageKey.FirefoxManifestVersion => "manifest_version is {0}; Firefox takes Manifest V2 or V3.",
        MessageKey.FirefoxManifestV2 => "Manifest V2: Firefox still supports it, Manifest V3 is recommended for new add-ons.",
        MessageKey.FirefoxNameMissing => "manifest.json has no name.",
        MessageKey.FirefoxTextTooLong => "{0} has {1} characters; AMO allows {2}.",
        MessageKey.FirefoxDefaultLocaleMissing => "The name or description is a __MSG_ placeholder but default_locale is missing.",
        MessageKey.FirefoxVersionInvalid => "version \"{0}\" is not one to four dot separated numbers of up to nine digits without leading zeros.",
        MessageKey.FirefoxVersionNotHigher => "version {0} must be higher than {1} ({2}).",
        MessageKey.FirefoxGeckoIdMissing => "browser_specific_settings.gecko.id is missing; Manifest V3 add-ons and updates of an existing add-on need it.",
        MessageKey.FirefoxGeckoIdInvalid => "browser_specific_settings.gecko.id \"{0}\" is not an e-mail like id or a braced UUID.",
        MessageKey.FirefoxGeckoIdMismatch => "browser_specific_settings.gecko.id is {0} but the add-on is {1}.",
        MessageKey.FirefoxDataCollectionMissing => "browser_specific_settings.gecko.data_collection_permissions is missing; AMO requires the data collection declaration (\"none\" when nothing is collected).",
        MessageKey.FirefoxUpdateUrl => "update_url points to {0}; listed versions are updated by AMO, the key is for self distributed (unlisted) versions.",
        MessageKey.FirefoxServiceWorker => "background has only a service_worker; Firefox runs background.scripts, add them next to it.",
        MessageKey.FirefoxIconMissing => "icons is empty; AMO shows a default icon.",
        MessageKey.FirefoxBroadHostAccess => "Broad host access ({0}) is reviewed closely; ask only for the hosts the add-on needs.",
        MessageKey.FirefoxSourceRequired => "{0} looks minified or bundled; AMO requires the source code and build instructions for such versions.",
        _ => key,
    };

    private static async Task<bool> HasLongLineAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = await entry.OpenAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[8192];
        var run = 0;
        int read;

        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            for (var index = 0; index < read; index++)
            {
                if (buffer[index] == '\n')
                {
                    run = 0;
                }
                else if (++run > MinifiedLineLength)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsMessage(string text) => text != null && text.StartsWith("__MSG_", StringComparison.Ordinal);

    private static JsonElement? Child(JsonElement? node, string name)
        => node is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static JsonElement? Child(JsonElement node, string name) => Child((JsonElement?)node, name);

    private static string Text(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Text(JsonElement? root, string name) => root is { } element ? Text(element, name) : null;

    private static IEnumerable<string> Strings(JsonElement root, string name) => ManifestIssueLines.Strings(root, name);

    private static IEnumerable<string> Strings(JsonElement? root, string name) => root is { } element ? Strings(element, name) : [];
}
