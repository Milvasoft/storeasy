using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// The parts of the winget manifest schema storeasy checks and edits (schema 1.12.0, the version wingetcreate writes;
/// the limits are the same in the later schema versions). Source: winget-cli <c>schemas/JSON/manifests</c> and
/// winget-pkgs <c>doc/manifest/schema</c>.
/// </summary>
public static partial class WingetFields
{
    /// <summary>
    /// Schema version storeasy writes.
    /// </summary>
    public const string DefaultManifestVersion = "1.12.0";

    /// <summary>
    /// Oldest schema version storeasy keeps when it rewrites a manifest; older ones are raised to
    /// <see cref="DefaultManifestVersion"/> (the pipeline labels deprecated versions <c>Manifest-Version-Deprecated</c>).
    /// </summary>
    public const string MinimumManifestVersion = "1.6.0";

    /// <summary>
    /// Fields of a locale manifest that storeasy edits as the package's "listing", with their limits (min, max length).
    /// </summary>
    public static IReadOnlyList<WingetTextField> ListingFields { get; } =
    [
        new("Publisher", 2, 256, RequiredInDefault: true),
        new("PackageName", 2, 256, RequiredInDefault: true),
        new("ShortDescription", 3, 256, RequiredInDefault: true),
        new("Description", 3, 10000),
        new("License", 3, 512, RequiredInDefault: true),
        new("Author", 2, 256),
        new("Copyright", 3, 512),
        new("Moniker", 1, 40, DefaultOnly: true),
        new("ReleaseNotes", 1, 10000),
        new("InstallationNotes", 1, 10000),
        new("PublisherUrl", 0, 2048, IsUrl: true),
        new("PublisherSupportUrl", 0, 2048, IsUrl: true),
        new("PrivacyUrl", 0, 2048, IsUrl: true),
        new("PackageUrl", 0, 2048, IsUrl: true),
        new("LicenseUrl", 0, 2048, IsUrl: true),
        new("CopyrightUrl", 0, 2048, IsUrl: true),
        new("ReleaseNotesUrl", 0, 2048, IsUrl: true),
        new("PurchaseUrl", 0, 2048, IsUrl: true),
    ];

    /// <summary>
    /// Every key of a locale manifest (used to split a singleton manifest).
    /// </summary>
    public static IReadOnlySet<string> LocaleFields { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "PackageLocale", "Publisher", "PublisherUrl", "PublisherSupportUrl", "PrivacyUrl", "Author", "PackageName", "PackageUrl", "License",
        "LicenseUrl", "Copyright", "CopyrightUrl", "ShortDescription", "Description", "Moniker", "Tags", "Agreements", "ReleaseNotes",
        "ReleaseNotesUrl", "PurchaseUrl", "InstallationNotes", "Documentations", "Icons",
    };

    /// <summary>
    /// Most tags of a locale manifest.
    /// </summary>
    public const int MaxTags = 16;

    /// <summary>
    /// Maximum length of one tag.
    /// </summary>
    public const int TagMaxLength = 40;

    /// <summary>
    /// <c>InstallerType</c> values of the schema.
    /// </summary>
    public static IReadOnlySet<string> InstallerTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "msix", "msi", "appx", "exe", "zip", "inno", "nullsoft", "wix", "burn", "pwa", "portable", "font" };

    /// <summary>
    /// Installer types the community repository accepts under <c>manifests/</c> (fonts live under <c>fonts/</c>; PWAs are
    /// not supported).
    /// </summary>
    public static IReadOnlySet<string> AllowedInstallerTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "msix", "msi", "appx", "exe", "zip", "inno", "nullsoft", "wix", "burn", "portable" };

    /// <summary>
    /// <c>Architecture</c> values.
    /// </summary>
    public static IReadOnlySet<string> Architectures { get; } = new HashSet<string>(StringComparer.Ordinal) { "x86", "x64", "arm", "arm64", "neutral" };

    /// <summary>
    /// <c>Scope</c> values.
    /// </summary>
    public static IReadOnlySet<string> Scopes { get; } = new HashSet<string>(StringComparer.Ordinal) { "user", "machine" };

    /// <summary>
    /// <c>InstallerSwitches</c> keys with their maximum length.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Switches { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Silent"] = 512,
        ["SilentWithProgress"] = 512,
        ["Interactive"] = 512,
        ["InstallLocation"] = 512,
        ["Log"] = 512,
        ["Upgrade"] = 512,
        ["Custom"] = 2048,
        ["Repair"] = 512,
    };

    /// <summary>
    /// Installer types that need silent switches in the manifest (the others carry their own: msi, wix, inno, nullsoft,
    /// burn, msix and portable).
    /// </summary>
    public static IReadOnlySet<string> NeedsSwitches { get; } = new HashSet<string>(StringComparer.Ordinal) { "exe" };

    /// <summary>
    /// <c>PackageIdentifier</c>: 2 to 8 dot separated segments of up to 32 characters, no white space or path characters.
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^[^\.\s\\/:\*\?""<>\|\x01-\x1f]{1,32}(\.[^\.\s\\/:\*\?""<>\|\x01-\x1f]{1,32}){1,7}$")]
    public static partial Regex PackageIdentifier();

    /// <summary>
    /// <c>PackageVersion</c>: no path characters.
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^[^\\/:\*\?""<>\|\x01-\x1f]+$")]
    public static partial Regex PackageVersion();

    /// <summary>
    /// <c>PackageLocale</c> (BCP 47 like).
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^([a-zA-Z]{2,3}|[iI]-[a-zA-Z]+|[xX]-[a-zA-Z]{1,8})(-[a-zA-Z]{1,8})*$")]
    public static partial Regex Locale();

    /// <summary>
    /// <c>InstallerSha256</c>.
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex("^[A-Fa-f0-9]{64}$")]
    public static partial Regex Sha256();

    /// <summary>
    /// A URL field (<c>http</c> or <c>https</c>).
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex("^([Hh][Tt][Tt][Pp][Ss]?)://.+$")]
    public static partial Regex Url();

    /// <summary>
    /// <c>ManifestVersion</c>: three numbers.
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(0|[1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])(\.(0|[1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])){2}$")]
    public static partial Regex ManifestVersion();

    /// <summary>
    /// <c>MinimumOSVersion</c>: up to four numbers.
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(0|[1-9][0-9]{0,4})(\.(0|[1-9][0-9]{0,4})){0,3}$")]
    public static partial Regex OsVersion();
}

/// <summary>
/// A text field of a locale manifest.
/// </summary>
/// <param name="Name">Manifest key.</param>
/// <param name="MinLength">Minimum length when present (0: none).</param>
/// <param name="MaxLength">Maximum length.</param>
/// <param name="RequiredInDefault">Required in the default locale manifest.</param>
/// <param name="DefaultOnly">Only meaningful in the default locale manifest (Moniker).</param>
/// <param name="IsUrl">An http(s) address.</param>
public sealed record WingetTextField(string Name, int MinLength, int MaxLength, bool RequiredInDefault = false, bool DefaultOnly = false, bool IsUrl = false);
