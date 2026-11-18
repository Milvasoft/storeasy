using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Result of the last check of a store connection.
/// </summary>
public enum ConnectionStatus : byte
{
    /// <summary>
    /// Saved but never checked.
    /// </summary>
    [Description("Not verified")]
    NotVerified = 1,

    /// <summary>
    /// The store accepted the credentials.
    /// </summary>
    [Description("Verified")]
    Verified = 2,

    /// <summary>
    /// The store rejected the credentials (401, 403, invalid key).
    /// </summary>
    [Description("Rejected")]
    Rejected = 3,

    /// <summary>
    /// The check could not reach a verdict (network error, store 5xx).
    /// </summary>
    [Description("Unreachable")]
    Unreachable = 4,
}
