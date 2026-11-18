using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Monetization;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// An In-App Purchase (App Store) or one-time product (Play) in the product list.
/// </summary>
public class IapListDto
{
    /// <summary>
    /// storeasy id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Product id.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Reference name or title.
    /// </summary>
    public string ReferenceName { get; set; }

    /// <summary>
    /// Kind.
    /// </summary>
    public StoreProductKind Kind { get; set; }

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
    /// Whether the App Review screenshot is uploaded (App Store).
    /// </summary>
    public bool HasReviewScreenshot { get; set; }

    /// <summary>
    /// Purchase options of a Play one-time product (id, type, state).
    /// </summary>
    public List<BasePlanDto> PurchaseOptions { get; set; }

    /// <summary>
    /// Base territory, read for <see cref="BasePrice"/>.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public string BaseTerritory { get; set; }

    /// <summary>
    /// Prices, read for <see cref="BasePrice"/>.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public List<ProductPrice> Prices { get; set; }

    /// <summary>
    /// Purchase options, read for <see cref="PurchaseOptions"/>.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public List<ProductPlan> Plans { get; set; }

    /// <summary>
    /// Projection from a product row; <see cref="BasePrice"/> and <see cref="PurchaseOptions"/> are filled after the read.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<StoreProduct, IapListDto>> Projection { get; } = p => new IapListDto
    {
        Id = p.Id,
        Store = p.Store,
        ProductId = p.ProductId,
        ReferenceName = p.ReferenceName,
        Kind = p.Kind,
        State = p.State,
        StoreState = p.StoreState,
        HasReviewScreenshot = p.ReviewScreenshot != null,
        BaseTerritory = p.BaseTerritory,
        Prices = p.Prices,
        Plans = p.Plans,
    };
}
