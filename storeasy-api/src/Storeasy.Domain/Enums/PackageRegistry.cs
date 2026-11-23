using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Package registries the package registries module watches (docs/urun-plani.md section 7.2). Separate from
/// <see cref="StoreKind"/>: a registry has no store page, screenshots or review process, and storeasy only reads its public
/// API (no credential is stored).
/// </summary>
public enum PackageRegistry : byte
{
    /// <summary>
    /// npm (registry.npmjs.org).
    /// </summary>
    [Description("npm")]
    Npm = 1,

    /// <summary>
    /// NuGet (nuget.org).
    /// </summary>
    [Description("NuGet")]
    NuGet = 2,

    /// <summary>
    /// PyPI (pypi.org).
    /// </summary>
    [Description("PyPI")]
    PyPI = 3,

    /// <summary>
    /// crates.io.
    /// </summary>
    [Description("crates.io")]
    Crates = 4,

    /// <summary>
    /// Maven Central (repo1.maven.org, search.maven.org); names are <c>groupId:artifactId</c>.
    /// </summary>
    [Description("Maven Central")]
    Maven = 5,

    /// <summary>
    /// RubyGems (rubygems.org).
    /// </summary>
    [Description("RubyGems")]
    RubyGems = 6,

    /// <summary>
    /// Packagist (packagist.org); names are <c>vendor/package</c>.
    /// </summary>
    [Description("Packagist")]
    Packagist = 7,

    /// <summary>
    /// pub.dev (Dart and Flutter packages).
    /// </summary>
    [Description("pub.dev")]
    Pub = 8,
}
