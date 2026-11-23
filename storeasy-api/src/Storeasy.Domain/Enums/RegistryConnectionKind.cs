using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Credentials the package registries module can hold (encrypted, never returned): a GitHub token to start releases in CI,
/// and registry API keys for registry write operations (unlist, deprecate, dist-tags, yank). Publishing itself stays in CI.
/// </summary>
public enum RegistryConnectionKind : byte
{
    /// <summary>
    /// A GitHub fine-grained personal access token (Contents, Actions and Metadata on the release repositories).
    /// </summary>
    [Description("GitHub")]
    GitHub = 1,

    /// <summary>
    /// A nuget.org API key with the unlist scope.
    /// </summary>
    [Description("NuGet API key")]
    NuGet = 2,

    /// <summary>
    /// An npm granular access token with read and write access.
    /// </summary>
    [Description("npm token")]
    Npm = 3,

    /// <summary>
    /// A crates.io API token with the yank scope.
    /// </summary>
    [Description("crates.io token")]
    Crates = 4,

    /// <summary>
    /// A RubyGems API key with the yank scope.
    /// </summary>
    [Description("RubyGems API key")]
    RubyGems = 5,
}
