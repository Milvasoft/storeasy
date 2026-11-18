using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Outcome of an inbound webhook event. Values are explicit and never reused.
/// </summary>
public enum WebhookProcessResult : byte
{
    /// <summary>Stored, waiting for processing.</summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>Processed (state updated, records opened or closed).</summary>
    [Description("Processed")]
    Processed = 2,

    /// <summary>Valid, but nothing to do (ping, unknown type, unknown app).</summary>
    [Description("Ignored")]
    Ignored = 3,

    /// <summary>Processing failed.</summary>
    [Description("Failed")]
    Failed = 4,

    /// <summary>The signature did not match; the event was kept but not processed.</summary>
    [Description("Invalid signature")]
    InvalidSignature = 5,

    /// <summary>The timestamp was outside the accepted window; kept but not processed.</summary>
    [Description("Stale")]
    Stale = 6,
}
