using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a release plan does.
/// </summary>
public enum ReleasePlanKind : byte
{
    /// <summary>
    /// Release type and phased release of an App Store version.
    /// </summary>
    [Description("Set release type")]
    SetReleaseType = 1,

    /// <summary>
    /// Put version codes on a Play track.
    /// </summary>
    [Description("Assign to track")]
    AssignTrack = 2,
}
