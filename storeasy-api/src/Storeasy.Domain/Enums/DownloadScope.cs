using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What the per version download counts of a registry cover.
/// </summary>
public enum DownloadScope : byte
{
    /// <summary>
    /// All time downloads of the version (NuGet, crates.io, RubyGems).
    /// </summary>
    [Description("All time")]
    Total = 1,

    /// <summary>
    /// Downloads of the version in the last 7 days (npm).
    /// </summary>
    [Description("Last 7 days")]
    LastWeek = 2,
}
