using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kind of a subscription offer.
/// </summary>
public enum OfferKind : byte
{
    /// <summary>
    /// App Store introductory offer (new subscribers, one per territory).
    /// </summary>
    [Description("Introductory offer")]
    Introductory = 1,

    /// <summary>
    /// App Store promotional offer (signed by the app's own server).
    /// </summary>
    [Description("Promotional offer")]
    Promotional = 2,

    /// <summary>
    /// App Store offer redeemed with offer codes.
    /// </summary>
    [Description("Offer code")]
    OfferCode = 3,

    /// <summary>
    /// App Store win-back offer (lapsed subscribers).
    /// </summary>
    [Description("Win-back offer")]
    WinBack = 4,

    /// <summary>
    /// Play subscription offer of a base plan.
    /// </summary>
    [Description("Play offer")]
    PlayOffer = 5,
}
