namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Listings, price tier and lifetime of an add-on. Null fields stay as they are.
/// </summary>
public class MsAddonContentDto
{
    /// <summary>
    /// Listings to set.
    /// </summary>
    public List<MsAddonListingDto> Listings { get; set; } = [];

    /// <summary>
    /// Price tier (Free, Tier2...).
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Lifetime of a durable add-on (Forever, OneDay, ThreeDays, FiveDays, OneWeek, TwoWeeks, OneMonth, TwoMonths, ThreeMonths, SixMonths, OneYear).
    /// </summary>
    public string Lifetime { get; set; }
}
