using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of the metrics feed of one store record. Values are explicit and never reused.
/// </summary>
public enum MetricFeedStatus : byte
{
    /// <summary>The store sends no reports for this record yet (App Store: analytics reports are not enabled).</summary>
    [Description("Not enabled")]
    NotEnabled = 0,

    /// <summary>Enabled; the store has not delivered the first data yet (App Store needs one to two days).</summary>
    [Description("Waiting for data")]
    Waiting = 1,

    /// <summary>Data arrives.</summary>
    [Description("Active")]
    Active = 2,

    /// <summary>A setting is missing (Google Play: the Cloud Storage bucket of the reports).</summary>
    [Description("Needs setup")]
    NeedsSetup = 3,

    /// <summary>The store refused the credentials (missing role, scope or bucket access).</summary>
    [Description("No access")]
    AccessDenied = 4,

    /// <summary>The last read failed for another reason; it is tried again.</summary>
    [Description("Failed")]
    Failed = 5,
}
