using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Whether the sender knows the endpoint. Values are explicit and never reused.
/// </summary>
public enum WebhookRegistrationState : byte
{
    /// <summary>Registration not done yet (a job is on the way, or a manual one is waiting for its first event).</summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>Registered at the store through its API.</summary>
    [Description("Registered through the API")]
    Api = 2,

    /// <summary>Registered by hand; the first valid event confirmed it.</summary>
    [Description("Registered by hand")]
    Manual = 3,

    /// <summary>The API registration failed (usually a key without the needed role).</summary>
    [Description("Registration failed")]
    Failed = 4,
}
