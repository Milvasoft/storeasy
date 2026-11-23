using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What an approved registry plan does.
/// </summary>
public enum RegistryPlanKind : byte
{
    /// <summary>
    /// Starts a release in CI (version bump, tag or workflow dispatch on GitHub).
    /// </summary>
    [Description("Release")]
    Release = 1,

    /// <summary>
    /// Unlists a NuGet version.
    /// </summary>
    [Description("Unlist")]
    Unlist = 2,

    /// <summary>
    /// Lists a NuGet version again.
    /// </summary>
    [Description("Relist")]
    Relist = 3,

    /// <summary>
    /// Deprecates an npm version with a message.
    /// </summary>
    [Description("Deprecate")]
    Deprecate = 4,

    /// <summary>
    /// Removes the deprecation of an npm version.
    /// </summary>
    [Description("Undeprecate")]
    Undeprecate = 5,

    /// <summary>
    /// Points an npm dist-tag at a version.
    /// </summary>
    [Description("Set dist-tag")]
    DistTag = 6,

    /// <summary>
    /// Yanks a crates.io or RubyGems version.
    /// </summary>
    [Description("Yank")]
    Yank = 7,

    /// <summary>
    /// Unyanks a crates.io version.
    /// </summary>
    [Description("Unyank")]
    Unyank = 8,
}
