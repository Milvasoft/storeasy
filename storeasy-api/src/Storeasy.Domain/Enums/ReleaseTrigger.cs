using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How a repository's CI starts a release, as storeasy starts it (docs/package-registries.md).
/// </summary>
public enum ReleaseTrigger : byte
{
    /// <summary>
    /// Commit a version bump to the version file(s) on the branch; a workflow watching those paths publishes.
    /// </summary>
    [Description("Version bump commit")]
    VersionBump = 1,

    /// <summary>
    /// Push a tag (for example <c>v{version}</c> or <c>cli-v{version}</c>) at the branch head; the tag is the version.
    /// </summary>
    [Description("Tag push")]
    Tag = 2,

    /// <summary>
    /// Commit a version bump, then push a tag on that commit (workflows that check the manifest against the tag).
    /// </summary>
    [Description("Version bump and tag")]
    VersionBumpAndTag = 3,

    /// <summary>
    /// Start a workflow with <c>workflow_dispatch</c> and the version as an input.
    /// </summary>
    [Description("Workflow dispatch")]
    Dispatch = 4,
}
