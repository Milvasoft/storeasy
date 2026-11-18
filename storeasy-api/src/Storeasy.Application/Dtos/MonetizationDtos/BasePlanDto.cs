namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A Play base plan with its offers.
/// </summary>
public class BasePlanDto
{
    /// <summary>
    /// Base plan id.
    /// </summary>
    public string BasePlanId { get; set; }

    /// <summary>
    /// AutoRenewing, Prepaid or Installments.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Billing period (ISO 8601).
    /// </summary>
    public string BillingPeriod { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ProductState State { get; set; }

    /// <summary>
    /// Price in the default region.
    /// </summary>
    public BasePriceDto BasePrice { get; set; }

    /// <summary>
    /// Number of regions with a price.
    /// </summary>
    public int RegionCount { get; set; }

    /// <summary>
    /// Offers of the base plan.
    /// </summary>
    public List<ProductOffer> Offers { get; set; }
}
