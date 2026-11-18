using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How a subscription offer is paid.
/// </summary>
public enum OfferMode : byte
{
    /// <summary>
    /// Free for a duration.
    /// </summary>
    [Description("Free trial")]
    FreeTrial = 1,

    /// <summary>
    /// A discounted price per period for a number of periods.
    /// </summary>
    [Description("Pay as you go")]
    PayAsYouGo = 2,

    /// <summary>
    /// One payment for the whole duration.
    /// </summary>
    [Description("Pay up front")]
    PayUpFront = 3,
}
