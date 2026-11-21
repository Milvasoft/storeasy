using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Whether a before and after report can be read yet. Values are explicit and never reused.
/// </summary>
public enum ImpactStatus : byte
{
    /// <summary>Both windows are over; the report is final (later store corrections aside).</summary>
    [Description("Ready")]
    Ready = 1,

    /// <summary>The after window has not ended yet; the numbers so far are shown.</summary>
    [Description("Collecting")]
    Collecting = 2,

    /// <summary>The change is not live yet (written to a version still in review or waiting for release).</summary>
    [Description("Awaiting release")]
    AwaitingRelease = 3,

    /// <summary>No store page metrics in either window (metrics not enabled, or older than the data).</summary>
    [Description("No data")]
    NoData = 4,
}
