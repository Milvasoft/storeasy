using System.Globalization;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// Credentials of the package registries module: the associated data their secrets are sealed with, what is shown instead
/// of a secret, and which credential each registry operation needs.
/// </summary>
public static class RegistrySecrets
{
    /// <summary>
    /// Characters of a key shown at the end of its fingerprint.
    /// </summary>
    public const int VisibleCharacters = 4;

    /// <summary>
    /// Associated data of a sealed credential (binds it to its organization and kind).
    /// </summary>
    public static string Context(int organizationId, RegistryConnectionKind kind) => string.Create(CultureInfo.InvariantCulture, $"RegistryConnection:{organizationId}:{kind}");

    /// <summary>
    /// The end of a key, never the key: <c>****abcd</c>.
    /// </summary>
    public static string Fingerprint(string secret) => secret.Length <= VisibleCharacters * 2 ? "****" : "****" + secret[^VisibleCharacters..];

    /// <summary>
    /// The registry a key is for; null for GitHub.
    /// </summary>
    public static PackageRegistry? RegistryOf(RegistryConnectionKind kind) => kind switch
    {
        RegistryConnectionKind.NuGet => PackageRegistry.NuGet,
        RegistryConnectionKind.Npm => PackageRegistry.Npm,
        RegistryConnectionKind.Crates => PackageRegistry.Crates,
        RegistryConnectionKind.RubyGems => PackageRegistry.RubyGems,
        _ => null,
    };

    /// <summary>
    /// The credential a registry's write operations need; null when storeasy has none for it.
    /// </summary>
    public static RegistryConnectionKind? KindOf(PackageRegistry registry) => registry switch
    {
        PackageRegistry.NuGet => RegistryConnectionKind.NuGet,
        PackageRegistry.Npm => RegistryConnectionKind.Npm,
        PackageRegistry.Crates => RegistryConnectionKind.Crates,
        PackageRegistry.RubyGems => RegistryConnectionKind.RubyGems,
        _ => null,
    };

    /// <summary>
    /// Display name of a credential kind.
    /// </summary>
    public static string DisplayName(RegistryConnectionKind kind) => kind switch
    {
        RegistryConnectionKind.GitHub => "GitHub token",
        RegistryConnectionKind.NuGet => "NuGet API key",
        RegistryConnectionKind.Npm => "npm token",
        RegistryConnectionKind.Crates => "crates.io token",
        _ => "RubyGems API key",
    };

    /// <summary>
    /// Whether <paramref name="registry"/> has an official API for <paramref name="kind"/> that storeasy performs: NuGet
    /// unlist and relist, npm deprecate, undeprecate and dist-tag, crates.io yank and unyank, RubyGems yank. PyPI yanks,
    /// Packagist abandons and NuGet deprecations are done on the registry's web site.
    /// </summary>
    public static bool Supports(PackageRegistry registry, RegistryPlanKind kind) => (registry, kind) switch
    {
        (PackageRegistry.NuGet, RegistryPlanKind.Unlist or RegistryPlanKind.Relist) => true,
        (PackageRegistry.Npm, RegistryPlanKind.Deprecate or RegistryPlanKind.Undeprecate or RegistryPlanKind.DistTag) => true,
        (PackageRegistry.Crates, RegistryPlanKind.Yank or RegistryPlanKind.Unyank) => true,
        (PackageRegistry.RubyGems, RegistryPlanKind.Yank) => true,
        _ => false,
    };
}
