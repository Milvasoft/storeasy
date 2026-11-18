using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How a rejection case was opened. Values are explicit and never reused.
/// </summary>
public enum RejectionSource : byte
{
    /// <summary>By an App Store Connect webhook event.</summary>
    [Description("Webhook")]
    Webhook = 1,

    /// <summary>By the periodic version state sync.</summary>
    [Description("Sync")]
    Sync = 2,

    /// <summary>By a member.</summary>
    [Description("Manual")]
    Manual = 3,
}
