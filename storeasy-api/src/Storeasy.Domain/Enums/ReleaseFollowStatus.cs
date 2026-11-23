using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Where a started release stands, as storeasy follows the CI run and the registry.
/// </summary>
public enum ReleaseFollowStatus : byte
{
    /// <summary>
    /// The CI run is queued or running, or the registry does not show the version yet.
    /// </summary>
    [Description("In progress")]
    Following = 1,

    /// <summary>
    /// Every package in scope shows the new version on its registry.
    /// </summary>
    [Description("Published")]
    Published = 2,

    /// <summary>
    /// The CI run failed.
    /// </summary>
    [Description("Run failed")]
    Failed = 3,

    /// <summary>
    /// The version did not appear within the follow window.
    /// </summary>
    [Description("Timed out")]
    TimedOut = 4,
}
