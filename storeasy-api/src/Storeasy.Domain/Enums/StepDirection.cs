using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Which way a store job step moves data, for the live activity view.
/// </summary>
public enum StepDirection : byte
{
    /// <summary>
    /// storeasy calls the store (requests, uploads).
    /// </summary>
    [Description("Outbound")]
    Outbound = 1,

    /// <summary>
    /// The store answers or reports back (responses, processing states).
    /// </summary>
    [Description("Inbound")]
    Inbound = 2,

    /// <summary>
    /// Work inside storeasy (matching, saving).
    /// </summary>
    [Description("Internal")]
    Internal = 3,
}
