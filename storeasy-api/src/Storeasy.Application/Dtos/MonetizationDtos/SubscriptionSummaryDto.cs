namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A subscription row of a group.
/// </summary>
public class SubscriptionSummaryDto
{
    /// <summary>
    /// storeasy id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Product id.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Reference name.
    /// </summary>
    public string ReferenceName { get; set; }

    /// <summary>
    /// Level in the group (1 is the highest).
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Subscription period.
    /// </summary>
    public string Duration { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ProductState State { get; set; }

    /// <summary>
    /// The store's state text.
    /// </summary>
    public string StoreState { get; set; }

    /// <summary>
    /// Base territory price.
    /// </summary>
    public BasePriceDto BasePrice { get; set; }

    /// <summary>
    /// Whether the App Review screenshot is uploaded.
    /// </summary>
    public bool HasReviewScreenshot { get; set; }
}
