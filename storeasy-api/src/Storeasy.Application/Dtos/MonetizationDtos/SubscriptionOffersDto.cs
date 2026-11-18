namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// The offers of a subscription by kind. One-time use codes are never stored: only their count is shown, the code file
/// is downloaded from App Store Connect.
/// </summary>
public class SubscriptionOffersDto
{
    /// <summary>
    /// storeasy id of the subscription.
    /// </summary>
    public long SubscriptionId { get; set; }

    /// <summary>
    /// Introductory offers (one per territory).
    /// </summary>
    public List<ProductOffer> Introductory { get; set; } = [];

    /// <summary>
    /// Promotional offers.
    /// </summary>
    public List<ProductOffer> Promotional { get; set; } = [];

    /// <summary>
    /// Offer codes.
    /// </summary>
    public List<ProductOffer> OfferCodes { get; set; } = [];

    /// <summary>
    /// Win-back offers.
    /// </summary>
    public List<ProductOffer> WinBack { get; set; } = [];

    /// <summary>
    /// Play offers of the base plans.
    /// </summary>
    public List<ProductOffer> PlayOffers { get; set; } = [];
}
