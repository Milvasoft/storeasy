using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a delivery rule does when the checks pass with warnings. Values are explicit and never reused.
/// </summary>
public enum DeliveryWarningAction : byte
{
    /// <summary>
    /// Deliver anyway.
    /// </summary>
    [Description("Deliver")]
    Deliver = 1,

    /// <summary>
    /// Hold the session for a member.
    /// </summary>
    [Description("Hold")]
    Hold = 2,
}
