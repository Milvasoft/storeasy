using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Where a delivery rule sends a package. Only test channels: production and review submission are never automatic. Values are explicit and never reused.
/// </summary>
public enum DeliveryTarget : byte
{
    /// <summary>
    /// An internal TestFlight group.
    /// </summary>
    [Description("TestFlight group")]
    TestFlightGroup = 1,

    /// <summary>
    /// A Google Play testing track (internal, alpha, beta or a closed track).
    /// </summary>
    [Description("Play track")]
    PlayTrack = 2,

    /// <summary>
    /// A Microsoft Store package flight.
    /// </summary>
    [Description("Package flight")]
    MsFlight = 3,

    /// <summary>
    /// Upload only, no target (or no automatic delivery).
    /// </summary>
    [Description("None")]
    None = 4,
}
