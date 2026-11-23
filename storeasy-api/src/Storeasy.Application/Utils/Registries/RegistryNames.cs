using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// Package name rules, addresses and ecosystem names of the package registries.
/// </summary>
public static partial class RegistryNames
{
    /// <summary>
    /// Longest NuGet package id.
    /// </summary>
    public const int NuGetMaxLength = 100;

    /// <summary>
    /// Longest crate name.
    /// </summary>
    public const int CrateMaxLength = 64;

    // npm: lower case, optionally scoped, no leading dot or underscore (validate-npm-package-name, new packages).
    [GeneratedRegex(@"^(?:@[a-z0-9~-][a-z0-9._~-]*/)?[a-z0-9~-][a-z0-9._~-]*$")]
    private static partial Regex NpmName();

    // NuGet: letters, digits and underscores, separated by single dots or dashes.
    [GeneratedRegex(@"^[A-Za-z0-9_]+(?:[.-][A-Za-z0-9_]+)*$")]
    private static partial Regex NuGetId();

    // PyPI: PEP 508 project names.
    [GeneratedRegex(@"^(?:[A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9._-]*[A-Za-z0-9])$")]
    private static partial Regex PyPIName();

    // crates.io: an ASCII letter first, then letters, digits, dashes and underscores.
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_-]*$")]
    private static partial Regex CrateName();

    [GeneratedRegex(@"[-_.]+")]
    private static partial Regex PyPISeparators();

    /// <summary>
    /// Whether <paramref name="name"/> (trimmed) is a valid package name on <paramref name="registry"/>.
    /// </summary>
    public static bool IsValid(PackageRegistry registry, string name)
    {
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
            return false;

        return registry switch
        {
            PackageRegistry.Npm => name.Length <= Domain.Registries.TrackedPackage.NameMaxLength && NpmName().IsMatch(name),
            PackageRegistry.NuGet => name.Length <= NuGetMaxLength && NuGetId().IsMatch(name),
            PackageRegistry.PyPI => name.Length <= Domain.Registries.TrackedPackage.NameMaxLength && PyPIName().IsMatch(name),
            PackageRegistry.Crates => name.Length <= CrateMaxLength && CrateName().IsMatch(name),
            _ => false,
        };
    }

    /// <summary>
    /// PEP 503 normalized PyPI name (lower case, runs of <c>-</c>, <c>_</c> and <c>.</c> as one <c>-</c>).
    /// </summary>
    public static string NormalizePyPI(string name) => PyPISeparators().Replace(name.Trim(), "-").ToLowerInvariant();

    /// <summary>
    /// The name as it goes into a registry API path: npm scopes keep their slash encoded (<c>@scope%2fname</c>), NuGet ids
    /// are lower case, PyPI names normalized.
    /// </summary>
    public static string PathName(PackageRegistry registry, string name) => registry switch
    {
        PackageRegistry.Npm => name.StartsWith('@') ? "@" + Uri.EscapeDataString(name[1..]).Replace("%2F", "%2f", StringComparison.Ordinal) : Uri.EscapeDataString(name),
        PackageRegistry.NuGet => Uri.EscapeDataString(name.ToLowerInvariant()),
        PackageRegistry.PyPI => Uri.EscapeDataString(NormalizePyPI(name)),
        _ => Uri.EscapeDataString(name),
    };

    /// <summary>
    /// The package's public page.
    /// </summary>
    public static string PageUrl(PackageRegistry registry, string name) => registry switch
    {
        PackageRegistry.Npm => $"https://www.npmjs.com/package/{name}",
        PackageRegistry.NuGet => $"https://www.nuget.org/packages/{name}",
        PackageRegistry.PyPI => $"https://pypi.org/project/{name}/",
        PackageRegistry.Crates => $"https://crates.io/crates/{name}",
        _ => null,
    };

    /// <summary>
    /// Display name of <paramref name="registry"/>.
    /// </summary>
    public static string DisplayName(PackageRegistry registry) => registry switch
    {
        PackageRegistry.Npm => "npm",
        PackageRegistry.NuGet => "NuGet",
        PackageRegistry.PyPI => "PyPI",
        PackageRegistry.Crates => "crates.io",
        _ => registry.ToString(),
    };

    /// <summary>
    /// The OSV.dev ecosystem of <paramref name="registry"/>.
    /// </summary>
    public static string OsvEcosystem(PackageRegistry registry) => registry switch
    {
        PackageRegistry.Npm => "npm",
        PackageRegistry.NuGet => "NuGet",
        PackageRegistry.PyPI => "PyPI",
        PackageRegistry.Crates => "crates.io",
        _ => null,
    };

    /// <summary>
    /// Whether the registry records provenance of a published version (NuGet does not).
    /// </summary>
    public static bool SupportsProvenance(PackageRegistry registry) => registry != PackageRegistry.NuGet;

    /// <summary>
    /// An advisory's page on osv.dev.
    /// </summary>
    public static string AdvisoryUrl(string id) => $"https://osv.dev/vulnerability/{Uri.EscapeDataString(id)}";
}
