using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How storeasy supports a kind of store listing experiment in a store.
/// </summary>
public enum AsoStoreSupport : byte
{
    /// <summary>
    /// Through the store's API, every write as a change plan.
    /// </summary>
    [Description("API")]
    Api = 1,

    /// <summary>
    /// The store has no API: storeasy prepares it and guides the member through the store console (assisted step).
    /// </summary>
    [Description("Assisted")]
    Assisted = 2,

    /// <summary>
    /// Not supported.
    /// </summary>
    [Description("Not supported")]
    NotSupported = 3,
}
