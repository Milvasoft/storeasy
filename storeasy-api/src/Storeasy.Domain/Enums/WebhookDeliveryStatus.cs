using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of one outgoing webhook delivery. Values are explicit and never reused.
/// </summary>
public enum WebhookDeliveryStatus : byte
{
    /// <summary>Waiting for its first attempt.</summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>The receiver answered 2xx.</summary>
    [Description("Delivered")]
    Succeeded = 2,

    /// <summary>Every attempt failed, or the address was refused.</summary>
    [Description("Failed")]
    Failed = 3,

    /// <summary>An attempt failed; another one is scheduled.</summary>
    [Description("Retrying")]
    Retrying = 4,
}
