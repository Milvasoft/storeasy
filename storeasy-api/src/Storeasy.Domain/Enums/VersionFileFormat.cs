using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Where the version sits in a version file a release bumps.
/// </summary>
public enum VersionFileFormat : byte
{
    /// <summary>
    /// MSBuild (<c>Directory.Build.props</c>, <c>.csproj</c>): the first <c>&lt;PackageVersion&gt;</c>, else <c>&lt;Version&gt;</c>.
    /// </summary>
    [Description("MSBuild property")]
    MsBuild = 1,

    /// <summary>
    /// <c>package.json</c>: the first <c>"version"</c>.
    /// </summary>
    [Description("package.json")]
    PackageJson = 2,

    /// <summary>
    /// TOML (<c>pyproject.toml</c>, <c>Cargo.toml</c>): the first <c>version = "..."</c> line.
    /// </summary>
    [Description("TOML version line")]
    Toml = 3,

    /// <summary>
    /// <c>.gemspec</c> or <c>version.rb</c>: the first <c>.version = "..."</c> or <c>VERSION = "..."</c>.
    /// </summary>
    [Description("Ruby version")]
    Ruby = 4,

    /// <summary>
    /// <c>pom.xml</c>: the project's own <c>&lt;version&gt;</c> (outside <c>&lt;parent&gt;</c>).
    /// </summary>
    [Description("Maven POM")]
    Pom = 5,

    /// <summary>
    /// A file that holds only the version (<c>VERSION</c>).
    /// </summary>
    [Description("Plain text")]
    PlainText = 6,
}
