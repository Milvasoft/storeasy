using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// A Play base plan of a subscription, or a purchase option of a one-time product: what is actually sold, with its
/// regional prices.
/// </summary>
public class ProductPlan
{
    /// <summary>
    /// Base plan id or purchase option id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// AutoRenewing, Prepaid, Installments (base plans); Buy, Rent (purchase options).
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Billing period (ISO 8601, e.g. P1M).
    /// </summary>
    public string BillingPeriod { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ProductState State { get; set; }

    /// <summary>
    /// Regional prices.
    /// </summary>
    public List<ProductPrice> Prices { get; set; } = [];

    /// <summary>
    /// Price for new regions in USD.
    /// </summary>
    public string OtherRegionsUsd { get; set; }

    /// <summary>
    /// Price for new regions in EUR.
    /// </summary>
    public string OtherRegionsEur { get; set; }
}
